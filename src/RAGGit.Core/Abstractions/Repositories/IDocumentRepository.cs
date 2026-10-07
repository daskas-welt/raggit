using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Core.Abstractions.Repositories;

/// <summary>
/// Repository for the <see cref="Document"/> aggregate root, per the
/// MS persistence-layer design (one repository per aggregate root).
/// <see cref="Chunk"/> rows are children of the Document aggregate and
/// are accessed only through this repository — never via their own repo.
/// Offline invariant: pure Microsoft.Data.Sqlite, no Ollama/LanceDB/HTTP.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Lists the caller's own documents (CreatedBy == sub, legacy excluded),
    /// ordered CreatedAt DESC, Id DESC.
    /// </summary>
    Task<DocumentsMinePage> ListMineAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Finds a document by id, or null when absent.
    /// </summary>
    Task<Document?> FindByIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all documents in the singleton library, newest first.
    /// </summary>
    Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a document by its SHA-256 hash, or null when absent.
    /// </summary>
    Task<Document?> FindByHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new document row.
    /// </summary>
    Task AddAsync(Document document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts chunk rows for a document in a single transaction.
    /// </summary>
    Task AddChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every chunk row of a document (030 re-process replaces stale
    /// single-level rows before persisting the two-level set).
    /// </summary>
    Task DeleteChunksAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads chunks by id (030 broad retrieval resolves distinct parent rows
    /// through this read). Missing ids are skipped.
    /// </summary>
    Task<IReadOnlyList<Chunk>> GetChunksByIdsAsync(
        IEnumerable<Guid> chunkIds,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists <see cref="DocumentStatus.Ready"/> documents that have no
    /// <see cref="ChunkLevel.Parent"/> row yet (030 backfill detection).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListReadyDocumentIdsWithoutParentChunkAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates a document's processing status and its failure explanation.
    /// <paramref name="failureReason"/> is written together with the status —
    /// null clears any previous reason, so a retried document does not keep a
    /// stale explanation.
    /// </summary>
    Task UpdateStatusAsync(
        Guid documentId,
        DocumentStatus status,
        string? failureReason = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes a document and its chunks. Returns true when the document
    /// existed and was deleted, false when it was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}
