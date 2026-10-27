using System;
using System.Collections.Generic;
using System.Linq;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// In-memory result of one query pipeline run (032, research R2/7). It is the
/// equivalent of the non-streaming <see cref="QueryResponse"/> plus the ids the
/// history row needs, so both transports persist and respond identically.
/// Nothing here is new persistence: the same fields are written to the same
/// <c>Queries</c> row as before.
/// </summary>
public sealed class QueryOutcome
{
    /// <summary>The grounded answer, or exactly <c>no relevant content found</c>.</summary>
    public string Answer { get; init; } = string.Empty;

    /// <summary>Citations in the order the answer cited them.</summary>
    public IReadOnlyList<Citation> Citations { get; init; } = Array.Empty<Citation>();

    /// <summary>
    /// Citation chunk ids exactly as produced by generation (the value the
    /// history row stores), preserved verbatim so persistence is unchanged.
    /// </summary>
    public IReadOnlyList<Guid> CitationIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Chunk ids retrieved for this query (empty when nothing matched).</summary>
    public IReadOnlyList<Guid> RetrievedChunkIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Whole-pipeline latency, measured exactly as before.</summary>
    public int LatencyMs { get; init; }

    /// <summary>Did-you-mean person hints when nothing was cited.</summary>
    public IReadOnlyList<PersonSuggestion> SuggestedPersons { get; init; } =
        Array.Empty<PersonSuggestion>();

    /// <summary>Projects onto the non-streaming response body.</summary>
    public QueryResponse ToResponse() =>
        new()
        {
            Answer = Answer,
            Citations = Citations.ToList(),
            RetrievedChunkIds = RetrievedChunkIds.ToList(),
            LatencyMs = LatencyMs,
            SuggestedPersons = SuggestedPersons.ToList(),
        };
}
