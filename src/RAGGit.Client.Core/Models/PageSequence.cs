using System;
using System.Collections.Generic;

namespace RAGGit.Client.Core.Models;

/// <summary>
/// One footer token: either a direct page jump or a collapsed gap.
/// </summary>
public abstract record PageToken;

/// <summary>
/// Direct jump to page <see cref="Page"/>; <see cref="IsCurrent"/> marks the active page.
/// </summary>
public sealed record PageNumberToken(int Page, bool IsCurrent) : PageToken;

/// <summary>
/// Collapsed gap between non-adjacent page numbers ("…").
/// </summary>
public sealed record EllipsisToken : PageToken;

/// <summary>
/// Pure page-sequence algorithm for the library footer: first and last pages are
/// always present, a window of nearby pages surrounds the current page, and gaps
/// collapse to single ellipsis tokens. Framework-free and unit-testable.
/// </summary>
public static class PageSequence
{
    /// <summary>
    /// Builds the ordered footer tokens for <paramref name="currentPage"/> of
    /// <paramref name="totalPages"/> (both 1-based; out-of-range input clamps).
    /// </summary>
    public static IReadOnlyList<PageToken> For(int currentPage, int totalPages)
    {
        var total = Math.Max(1, totalPages);
        var current = Math.Clamp(currentPage, 1, total);

        var numbers = new SortedSet<int> { 1, total };
        for (var page = current - 2; page <= current + 2; page++)
        {
            if (page >= 1 && page <= total)
            {
                numbers.Add(page);
            }
        }

        var tokens = new List<PageToken>();
        var previous = 0;
        foreach (var page in numbers)
        {
            if (previous != 0 && page - previous > 1)
            {
                tokens.Add(new EllipsisToken());
            }

            tokens.Add(new PageNumberToken(page, page == current));
            previous = page;
        }

        return tokens;
    }
}
