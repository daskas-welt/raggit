using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;

namespace RAGGit.Ingest;

/// <summary>
/// Orchestrates document ingestion: hash dedupe, text extraction, chunking,
/// embedding, vector-store upsert, and SQLite metadata persistence.
/// </summary>
public sealed class IngestService
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly RagDbContext _db;
    private readonly ILogger<IngestService> _logger;
    private readonly IngestOptions _options;
    private readonly IMemoryCache? _chunkCache;
    private readonly IDocumentContentStore _contentStore;

    public IngestService(
        IEmbedder embedder,
        IVectorStore vectorStore,
        RagDbContext db,
        ILogger<IngestService> logger,
        IDocumentContentStore contentStore,
        IOptions<IngestOptions>? options = null,
        IMemoryCache? chunkCache = null
    )
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new IngestOptions();
        _chunkCache = chunkCache;
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
    }

    /// <summary>
    /// Ingests a document into the library. Returns the existing document and
    /// <c>false</c> when a document with the same hash already exists.
    /// </summary>
    public async Task<(Document Document, bool Created)> IngestAsync(
        Stream content,
        string filename,
        DocumentMimeType mime,
        long size,
        string createdBy,
        string? createdByName = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var hash = await Chunker.ComputeHashAsync(content);

        var existing = await FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Duplicate hash detected for {Filename}; returning existing document {DocumentId}",
                filename,
                existing.Id
            );
            return (existing, false);
        }

        // For xlsx, validate extraction + cap + empty BEFORE creating Document row so rejections never persist a row (per data-model.md)
        if (mime == DocumentMimeType.Xlsx)
        {
            if (content.CanSeek && content.Position != 0)
                content.Position = 0;
            var preview = await Chunker.ExtractTextAsync(content, mime);
            if (string.IsNullOrWhiteSpace(preview))
                throw new NoExtractableContentException("no extractable content");
            // Also cap already thrown as SpreadsheetCellCapExceededException during preview
            if (content.CanSeek)
                content.Position = 0;
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Filename = filename,
            Mime = mime,
            Size = size,
            Hash = hash,
            Status = DocumentStatus.Indexing,
            CreatedBy = createdBy,
            CreatedByName = createdByName,
        };

        await InsertDocumentAsync(document, cancellationToken);
        _logger.LogInformation(
            "Started ingest for document {DocumentId} ({Filename})",
            document.Id,
            filename
        );

        // Persist the original bytes for later download (009). Rewind first:
        // hashing/preview may have consumed the stream. Failed-ingest rows keep
        // their bytes (downloadable); rollback paths below delete them again.
        if (content.CanSeek)
        {
            content.Position = 0;
        }
        await _contentStore.SaveAsync(document.Id, content, cancellationToken);

        try
        {
            if (content.CanSeek && content.Position != 0)
            {
                content.Position = 0;
            }

            var chunks = await GetOrCreateChunksAsync(content, mime, document, cancellationToken);
            if (mime == DocumentMimeType.Xlsx && chunks.Count == 0)
                throw new NoExtractableContentException("no extractable content");
            _logger.LogInformation(
                "Document {DocumentId} produced {ChunkCount} chunks",
                document.Id,
                chunks.Count
            );

            if (chunks.Count > 0)
            {
                var embeddings = await EmbedInBatchesAsync(
                    chunks,
                    _options.EmbedBatchSize,
                    cancellationToken
                );

                var records = chunks
                    .Select(
                        (chunk, index) =>
                            new VectorRecord(
                                chunk.Id,
                                embeddings[index],
                                new Dictionary<string, object?>
                                {
                                    ["documentId"] = document.Id.ToString(),
                                    ["text"] = chunk.Text,
                                    ["ordinal"] = chunk.Ordinal,
                                }
                            )
                    )
                    .ToList();

                await _vectorStore.UpsertAsync(records, cancellationToken);
                await InsertChunksAsync(chunks, cancellationToken);
            }

            document.Status = DocumentStatus.Ready;
            await UpdateDocumentStatusAsync(document.Id, DocumentStatus.Ready, cancellationToken);
            _logger.LogInformation("Completed ingest for document {DocumentId}", document.Id);

            return (document, true);
        }
        catch (CorruptDocumentException)
        {
            _logger.LogWarning(
                "Corrupt document {DocumentId} ({Filename}) — rolling back",
                document.Id,
                filename
            );
            await DeleteDocumentRowAsync(document.Id, cancellationToken);
            // Ensure no vectors remain for this document (best-effort)
            try
            {
                await _vectorStore.DeleteAsync(document.Id.ToString(), cancellationToken);
            }
            catch { }

            await DeleteStoredOriginalBestEffortAsync(document.Id, cancellationToken);
            throw;
        }
        catch (SpreadsheetCellCapExceededException)
        {
            _logger.LogWarning(
                "Spreadsheet cap exceeded for {Filename} — rolling back document {DocumentId}",
                filename,
                document.Id
            );
            await DeleteDocumentRowAsync(document.Id, cancellationToken);
            try
            {
                await _vectorStore.DeleteAsync(document.Id.ToString(), cancellationToken);
            }
            catch { }

            await DeleteStoredOriginalBestEffortAsync(document.Id, cancellationToken);
            throw;
        }
        catch (NoExtractableContentException)
        {
            _logger.LogWarning(
                "No extractable content for {Filename} — rolling back document {DocumentId}",
                filename,
                document.Id
            );
            await DeleteDocumentRowAsync(document.Id, cancellationToken);
            try
            {
                await _vectorStore.DeleteAsync(document.Id.ToString(), cancellationToken);
            }
            catch { }

            await DeleteStoredOriginalBestEffortAsync(document.Id, cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ingest failed for document {DocumentId}", document.Id);
            await UpdateDocumentStatusAsync(document.Id, DocumentStatus.Failed, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Finds a document by id, or null when absent. Used by the download
    /// endpoint to resolve filename/MIME before serving stored bytes.
    /// </summary>
    public async Task<Document?> FindDocumentByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt
            FROM Documents
            WHERE Id = @id
            LIMIT 1;";
        command.Parameters.AddWithValue("@id", documentId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDocument(reader);
        }

        return null;
    }

    /// <summary>
    /// Lists all documents in the singleton library, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt
            FROM Documents
            ORDER BY CreatedAt DESC;";

        var documents = new List<Document>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    /// <summary>
    /// Deletes a document and its chunks from SQLite and purges its vectors
    /// from the configured vector store. Returns <c>true</c> when the document
    /// existed and was deleted, <c>false</c> when it was not found.
    /// </summary>
    public async Task<bool> DeleteDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            using var existsCommand = connection.CreateCommand();
            existsCommand.Transaction = (SqliteTransaction)transaction;
            existsCommand.CommandText = "SELECT COUNT(*) FROM Documents WHERE Id = @id;";
            existsCommand.Parameters.AddWithValue("@id", documentId.ToString());
            var count = Convert.ToInt64(await existsCommand.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            using var deleteChunksCommand = connection.CreateCommand();
            deleteChunksCommand.Transaction = (SqliteTransaction)transaction;
            deleteChunksCommand.CommandText = "DELETE FROM Chunks WHERE DocumentId = @id;";
            deleteChunksCommand.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteChunksCommand.ExecuteNonQueryAsync(cancellationToken);

            using var deleteDocumentCommand = connection.CreateCommand();
            deleteDocumentCommand.Transaction = (SqliteTransaction)transaction;
            deleteDocumentCommand.CommandText = "DELETE FROM Documents WHERE Id = @id;";
            deleteDocumentCommand.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteDocumentCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await _vectorStore.DeleteAsync(documentId.ToString(), cancellationToken);
        await DeleteStoredOriginalBestEffortAsync(documentId, cancellationToken);
        _logger.LogInformation(
            "Deleted document {DocumentId} and purged its vectors and stored original",
            documentId
        );
        return true;
    }

    /// <summary>
    /// Best-effort stored-original removal (rollback and delete paths must never
    /// fail because of orphan-byte cleanup).
    /// </summary>
    private async Task DeleteStoredOriginalBestEffortAsync(
        Guid documentId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _contentStore.DeleteAsync(documentId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Stored-original cleanup failed for document {DocumentId}",
                documentId
            );
        }
    }

    /// <summary>
    /// Finds a document by its SHA-256 hash.
    /// </summary>
    public async Task<Document?> FindByHashAsync(
        string hash,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt
            FROM Documents
            WHERE Hash = @hash
            LIMIT 1;";
        command.Parameters.AddWithValue("@hash", hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDocument(reader);
        }

        return null;
    }

    private async Task InsertDocumentAsync(Document document, CancellationToken cancellationToken)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt)
            VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdByName, @createdAt);";

        command.Parameters.AddWithValue("@id", document.Id.ToString());
        command.Parameters.AddWithValue("@filename", document.Filename);
        command.Parameters.AddWithValue("@mime", document.Mime.GetContentType());
        command.Parameters.AddWithValue("@size", document.Size);
        command.Parameters.AddWithValue("@hash", document.Hash);
        command.Parameters.AddWithValue("@status", document.Status.ToString());
        command.Parameters.AddWithValue("@createdBy", document.CreatedBy);
        command.Parameters.AddWithValue(
            "@createdByName",
            document.CreatedByName is null ? DBNull.Value : document.CreatedByName
        );
        command.Parameters.AddWithValue("@createdAt", document.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertChunksAsync(
        IEnumerable<Chunk> chunks,
        CancellationToken cancellationToken
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var chunk in chunks)
            {
                using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText =
                    @"
                    INSERT INTO Chunks (Id, DocumentId, Ordinal, Text, TokenCount)
                    VALUES (@id, @documentId, @ordinal, @text, @tokenCount);";

                command.Parameters.AddWithValue("@id", chunk.Id.ToString());
                command.Parameters.AddWithValue("@documentId", chunk.DocumentId.ToString());
                command.Parameters.AddWithValue("@ordinal", chunk.Ordinal);
                command.Parameters.AddWithValue("@text", chunk.Text);
                command.Parameters.AddWithValue("@tokenCount", chunk.TokenCount);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task UpdateDocumentStatusAsync(
        Guid documentId,
        DocumentStatus status,
        CancellationToken cancellationToken
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Documents SET Status = @status WHERE Id = @id;";
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@id", documentId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task DeleteDocumentRowAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            using var deleteChunks = connection.CreateCommand();
            deleteChunks.Transaction = (SqliteTransaction)transaction;
            deleteChunks.CommandText = "DELETE FROM Chunks WHERE DocumentId = @id;";
            deleteChunks.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteChunks.ExecuteNonQueryAsync(cancellationToken);

            using var deleteDoc = connection.CreateCommand();
            deleteDoc.Transaction = (SqliteTransaction)transaction;
            deleteDoc.CommandText = "DELETE FROM Documents WHERE Id = @id;";
            deleteDoc.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteDoc.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlyList<Chunk>> GetOrCreateChunksAsync(
        Stream content,
        DocumentMimeType mime,
        Document document,
        CancellationToken cancellationToken
    )
    {
        var cacheKey = $"chunks:{document.Hash}";
        if (
            _options.EnableChunkCache
            && _chunkCache is not null
            && _chunkCache.TryGetValue(cacheKey, out IReadOnlyList<Chunk>? cached)
            && cached is not null
        )
        {
            _logger.LogDebug("Chunk cache hit for document {DocumentId}", document.Id);
            return cached
                .Select(c => new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Ordinal = c.Ordinal,
                    Text = c.Text,
                    TokenCount = c.TokenCount,
                })
                .ToList();
        }

        var text = await Chunker.ExtractTextAsync(content, mime);
        var chunks = Chunker.ChunkText(
            text,
            document.Id,
            _options.ChunkSize,
            _options.ChunkOverlap
        );

        if (_options.EnableChunkCache && _chunkCache is not null)
        {
            _chunkCache.Set(
                cacheKey,
                chunks,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.ChunkCacheTtl,
                    Size = chunks.Count,
                }
            );
        }

        return chunks;
    }

    private async Task<IReadOnlyList<float[]>> EmbedInBatchesAsync(
        IReadOnlyList<Chunk> chunks,
        int batchSize,
        CancellationToken cancellationToken
    )
    {
        batchSize = Math.Max(1, batchSize);
        var allEmbeddings = new List<float[]>(chunks.Count);

        for (var i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).Select(c => c.Text).ToList();
            var embeddings = await _embedder.GetEmbeddingsAsync(batch, cancellationToken);

            if (embeddings.Count != batch.Count)
            {
                throw new InvalidOperationException(
                    $"Embedder returned {embeddings.Count} vectors for {batch.Count} chunks."
                );
            }

            allEmbeddings.AddRange(embeddings);
        }

        return allEmbeddings;
    }

    private static Document MapDocument(SqliteDataReader reader)
    {
        var mimeText = reader.GetString(2);
        var mime = mimeText switch
        {
            "application/pdf" => DocumentMimeType.Pdf,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                DocumentMimeType.Docx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                DocumentMimeType.Xlsx,
            "text/plain" => DocumentMimeType.Txt,
            "text/markdown" => DocumentMimeType.Md,
            _ => throw new InvalidOperationException($"Unknown MIME type in database: {mimeText}"),
        };

        if (!Enum.TryParse<DocumentStatus>(reader.GetString(5), out var status))
        {
            throw new InvalidOperationException(
                $"Unknown status in database: {reader.GetString(5)}"
            );
        }

        return new Document
        {
            Id = Guid.Parse(reader.GetString(0)),
            Filename = reader.GetString(1),
            Mime = mime,
            Size = reader.GetInt64(3),
            Hash = reader.GetString(4),
            Status = status,
            CreatedBy = reader.GetString(6),
            CreatedByName = reader.IsDBNull(7) ? null : reader.GetString(7),
            CreatedAt = DateTime.Parse(reader.GetString(8)),
        };
    }
}
