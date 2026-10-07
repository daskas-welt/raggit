using System;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// Deterministic, rule-based query-intent classifier (030, research R2).
/// Any broad trigger phrase in the query ⇒ <see cref="QueryIntent.Broad"/>;
/// otherwise ⇒ <see cref="QueryIntent.Granular"/> (the safe default per
/// FR-008, identical to pre-feature behaviour). Pure string matching:
/// no LLM, no I/O, no query-time network access (offline invariant),
/// O(query length) so the latency envelope (SC-005) is preserved.
/// </summary>
public static class QueryIntentClassifier
{
    private static readonly string[] BroadTriggers = new[]
    {
        "summarize",
        "summary",
        "overview",
        "compare",
        "comparison",
        "contrast",
        "difference between",
        "differences between",
        "list all",
        "all the steps",
        "end-to-end",
        "explain how",
        "walk me through",
        "pros and cons",
        "high-level",
        "guide",
    };

    /// <summary>
    /// Classifies <paramref name="query"/> as broad or granular.
    /// Null, empty, or whitespace ⇒ <see cref="QueryIntent.Granular"/>.
    /// </summary>
    public static QueryIntent Classify(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return QueryIntent.Granular;
        }

        foreach (var trigger in BroadTriggers)
        {
            if (query.Contains(trigger, StringComparison.OrdinalIgnoreCase))
            {
                return QueryIntent.Broad;
            }
        }

        return QueryIntent.Granular;
    }
}
