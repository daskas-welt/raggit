using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// Abstraction over an embedded vector store (e.g. LanceDB local path).
/// </summary>
public interface IVectorStore
{
    Task UpsertAsync(
        IEnumerable<VectorRecord> vectors,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit,
        string? documentIdFilter = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the vector store is reachable and operational.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
