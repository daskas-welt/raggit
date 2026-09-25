using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Data;
using RAGGit.Core.Models;

namespace RAGGit.Ingest;

/// <summary>
/// Orchestrates document ingestion: hash dedupe, text extraction, chunking,
/// embedding, vector-store upsert, and SQLite metadata persistence (via
/// <see cref="IDocumentRepository"/> — the single channel for the Document
/// aggregate per the MS persistence-layer design).
/// </summary>
public sealed class IngestService
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<IngestService> _logger;
    private readonly IngestOptions _options;
    private readonly IMemoryCache? _chunkCache;
    private readonly IDocumentContentStore _contentStore;

    public IngestService(
        IEmbedder embedder,
        IVectorStore vectorStore,
        IDocumentRepository documents,
        ILogger<IngestService> logger,
        IDocumentContentStore contentStore,
        IOptions<IngestOptions>? options = null,
        IMemoryCache? chunkCache = null
    )
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
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
            var preview = await Chunker.ExtractTextAsync(
                content,
                mime,
                _options.MaxSpreadsheetCells
            );
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
    public Task<Document?> FindDocumentByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    ) => _documents.FindByIdAsync(documentId, cancellationToken);

    /// <summary>
    /// Lists all documents in the singleton library, newest first.
    /// </summary>
    public Task<IReadOnlyList<Document>> ListDocumentsAsync(
        CancellationToken cancellationToken = default
    ) => _documents.ListAsync(cancellationToken);

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
        var deleted = await _documents.DeleteAsync(documentId, cancellationToken);
        if (!deleted)
        {
            return false;
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
    public Task<Document?> FindByHashAsync(
        string hash,
        CancellationToken cancellationToken = default
    ) => _documents.FindByHashAsync(hash, cancellationToken);

    private Task InsertDocumentAsync(Document document, CancellationToken cancellationToken) =>
        _documents.AddAsync(document, cancellationToken);

    private Task InsertChunksAsync(
        IEnumerable<Chunk> chunks,
        CancellationToken cancellationToken
    ) => _documents.AddChunksAsync(chunks, cancellationToken);

    private Task UpdateDocumentStatusAsync(
        Guid documentId,
        DocumentStatus status,
        CancellationToken cancellationToken
    ) => _documents.UpdateStatusAsync(documentId, status, cancellationToken);

    private Task DeleteDocumentRowAsync(Guid documentId, CancellationToken cancellationToken) =>
        _documents.DeleteAsync(documentId, cancellationToken);

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

        var text = await Chunker.ExtractTextAsync(content, mime, _options.MaxSpreadsheetCells);
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
}
