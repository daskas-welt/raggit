namespace RAGGit.Core.Models;

/// <summary>
/// Tunable thresholds for the query retrieval pipeline.
/// Values are loaded from configuration (<c>Retrieval</c> section).
/// </summary>
public sealed class RetrievalOptions
{
    /// <summary>
    /// Chunks retrieved and fed to the model when the request does not ask for
    /// a specific depth. Measured: 3 chunks suffice on a small form library
    /// (43 chunks) but lose retrievable answers on a large one (2933 chunks),
    /// so the shipped default is 5. Lower it to 3 only when prompt processing,
    /// not recall, is the bottleneck.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum cosine-similarity score for a chunk to be considered relevant.
    /// LanceDB returns cosine distance (0 = identical); the service converts
    /// it to similarity via <c>score = 1 - distance</c>.
    /// The previous hardcoded value (0.01) passed nearly everything, forcing
    /// cross-person pollution on small libraries. Default 0.25 is a starting
    /// point for small local libraries — operators should raise it (e.g. 0.35)
    /// if cross-document pollution persists. Comparison is inclusive.
    /// </summary>
    public float MinScore { get; set; } = 0.25f;

    /// <summary>
    /// Broad-retrieval fan-out (030): child candidates fetched for a broad
    /// query = <c>topK × BroadCandidateMultiplier</c>, capped at
    /// <see cref="MaxCandidates"/>. Default 4.
    /// </summary>
    public int BroadCandidateMultiplier { get; set; } = 4;

    /// <summary>
    /// Upper bound on the broad-retrieval child candidate fetch (030).
    /// Default 50.
    /// </summary>
    public int MaxCandidates { get; set; } = 50;
}
