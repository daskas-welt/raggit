using System;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// <summary>
    /// Broad cue phrases. Matched on word boundaries (see
    /// <see cref="BroadPattern"/>) so a short cue such as <c>guide</c> does
    /// not fire inside an unrelated word (<c>guideline</c>). The list carries
    /// the common inflections explicitly, because word-boundary matching does
    /// not match a longer form of a shorter cue.
    /// </summary>
    private static readonly string[] BroadTriggers = new[]
    {
        "summarize",
        "summarizes",
        "summarized",
        "summarizing",
        "summary",
        "summaries",
        "overview",
        "overviews",
        "compare",
        "compares",
        "compared",
        "comparing",
        "comparison",
        "comparisons",
        "contrast",
        "contrasts",
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
        "guides",
    };

    private static readonly Regex BroadPattern = new(
        @"\b(?:" + string.Join("|", BroadTriggers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

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

        return BroadPattern.IsMatch(query) ? QueryIntent.Broad : QueryIntent.Granular;
    }
}
