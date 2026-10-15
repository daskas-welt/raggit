using System.Collections.Generic;

namespace RAGGit.Core.Models;

/// <summary>
/// One employee-facing statement of work in progress on the query stream (032).
/// The workstation renders <see cref="Text"/> in the question's language, so the
/// client stays a thin renderer (research R4). This type is never persisted
/// (FR-014) — it exists only for the duration of a wait.
/// </summary>
public sealed class QueryProgress
{
    /// <summary>The work being described.</summary>
    public QueryStage Stage { get; set; }

    /// <summary>
    /// Plain-language sentence for the employee. Contains no prompt text, chunk
    /// body, internal identifier, model configuration, or infrastructure name.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Number of distinct relevant sources found. Populated only for
    /// <see cref="QueryStage.Sources"/>, and never when retrieval found nothing.
    /// </summary>
    public int? SourceCount { get; set; }

    /// <summary>
    /// Filenames of the sources found, already surfaced by the answer's own
    /// citations. Populated only for <see cref="QueryStage.Sources"/>.
    /// </summary>
    public List<string>? DocumentNames { get; set; }
}
