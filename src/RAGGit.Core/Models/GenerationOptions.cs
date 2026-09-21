namespace RAGGit.Core.Models;

/// <summary>
/// Tunable options for grounded answer generation.
/// Values are loaded from configuration (<c>Generation</c> section).
/// </summary>
public sealed class GenerationOptions
{
    /// <summary>
    /// When the LLM emits no explicit <c>[guid]</c> citations, cite only the
    /// single highest-scoring retrieved chunk instead of all of them.
    /// This preserves SC-004 citation coverage while avoiding citations to
    /// documents irrelevant to the question (e.g. same-template records
    /// about a different person). Default: true.
    /// Set to false for strict mode (no fallback citations at all).
    /// </summary>
    public bool FallbackToTopChunk { get; set; } = true;

    /// <summary>
    /// Minimum similarity score the top chunk must have for the fallback
    /// citation to apply. Below this floor even the best chunk is too weak
    /// to vouch for, so no fallback citation is emitted. Sits just above the
    /// retrieval MinScore (0.25) so borderline chunks stay uncited while
    /// moderately relevant ones keep SC-004 coverage. Default: 0.3.
    /// </summary>
    public float FallbackMinScore { get; set; } = 0.3f;
}
