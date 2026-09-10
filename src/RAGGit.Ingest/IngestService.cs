using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
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
    private readonly int _chunkSize;
    private readonly int _overlap;

    public IngestService(
        IEmbedder embedder,
        IVectorStore vectorStore,
        RagDbContext db,
        ILogger<IngestService> logger,
        int chunkSize = 512,
        int overlap = 50)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _chunkSize = chunkSize > 0 ? chunkSize : 512;
        _overlap = overlap >= 0 && overlap < _chunkSize ? overlap : 50;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var hash = await Chunker.ComputeHashAsync(content);

        var existing = await FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Duplicate hash detected for {Filename}; returning existing document {DocumentId}", filename, existing.Id);
            return (existing, false);
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Filename = filename,
            Mime = mime,
            Size = size,
            Hash = hash,
            Status = DocumentStatus.Indexing,
            CreatedBy = createdBy
        };

        await InsertDocumentAsync(document, cancellationToken);
        _logger.LogInformation("Started ingest for document {DocumentId} ({Filename})", document.Id, filename);

        try
        {
            if (content.CanSeek && content.Position != 0)
            {
                content.Position = 0;
            }

            var text = await Chunker.ExtractTextAsync(content, mime);
            var chunks = Chunker.ChunkText(text, document.Id, _chunkSize, _overlap);
            _logger.LogInformation("Document {DocumentId} produced {ChunkCount} chunks", document.Id, chunks.Count);

            if (chunks.Count > 0)
            {
                var embeddings = await _embedder.GetEmbeddingsAsync(chunks.Select(c => c.Text), cancellationToken);
                if (embeddings.Count != chunks.Count)
                {
                    throw new InvalidOperationException($"Embedder returned {embeddings.Count} vectors for {chunks.Count} chunks.");
                }

                var records = chunks.Select((chunk, index) => new VectorRecord(
                    chunk.Id,
                    embeddings[index],
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = document.Id.ToString(),
                        ["text"] = chunk.Text,
                        ["ordinal"] = chunk.Ordinal
                    })).ToList();

                await _vectorStore.UpsertAsync(records, cancellationToken);
                await InsertChunksAsync(chunks, cancellationToken);
            }

            document.Status = DocumentStatus.Ready;
            await UpdateDocumentStatusAsync(document.Id, DocumentStatus.Ready, cancellationToken);
            _logger.LogInformation("Completed ingest for document {DocumentId}", document.Id);

            return (document, true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Ingest failed for document {DocumentId}", document.Id);
            await UpdateDocumentStatusAsync(document.Id, DocumentStatus.Failed, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Lists all documents in the singleton library, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt
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
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
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
        _logger.LogInformation("Deleted document {DocumentId} and purged its vectors", documentId);
        return true;
    }

    /// <summary>
    /// Finds a document by its SHA-256 hash.
    /// </summary>
    public async Task<Document?> FindByHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt
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
        command.CommandText = @"
            INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
            VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt);";

        command.Parameters.AddWithValue("@id", document.Id.ToString());
        command.Parameters.AddWithValue("@filename", document.Filename);
        command.Parameters.AddWithValue("@mime", document.Mime.GetContentType());
        command.Parameters.AddWithValue("@size", document.Size);
        command.Parameters.AddWithValue("@hash", document.Hash);
        command.Parameters.AddWithValue("@status", document.Status.ToString());
        command.Parameters.AddWithValue("@createdBy", document.CreatedBy);
        command.Parameters.AddWithValue("@createdAt", document.CreatedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken)
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
                command.CommandText = @"
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

    private async Task UpdateDocumentStatusAsync(Guid documentId, DocumentStatus status, CancellationToken cancellationToken)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Documents SET Status = @status WHERE Id = @id;";
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@id", documentId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Document MapDocument(SqliteDataReader reader)
    {
        var mimeText = reader.GetString(2);
        var mime = mimeText switch
        {
            "application/pdf" => DocumentMimeType.Pdf,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => DocumentMimeType.Docx,
            "text/plain" => DocumentMimeType.Txt,
            "text/markdown" => DocumentMimeType.Md,
            _ => throw new InvalidOperationException($"Unknown MIME type in database: {mimeText}")
        };

        if (!Enum.TryParse<DocumentStatus>(reader.GetString(5), out var status))
        {
            throw new InvalidOperationException($"Unknown status in database: {reader.GetString(5)}");
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
            CreatedAt = DateTime.Parse(reader.GetString(7))
        };
    }
}
