namespace RAGGit.Core.Models;

/// <summary>
/// Tunable thresholds for the query retrieval pipeline.
/// Values are loaded from configuration (<c>Retrieval</c> section).
/// </summary>
public sealed class RetrievalOptions
{
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
}
