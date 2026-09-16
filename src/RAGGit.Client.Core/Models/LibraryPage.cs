using System;
using System.Collections.Generic;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>
/// One page window onto the fetched library list: the item slice plus the
/// echoed, clamped paging values the footer binds to. Built via
/// <see cref="Create"/>; never fetched per page (page turns slice in memory).
/// </summary>
public sealed class LibraryPage
{
    public IReadOnlyList<Document> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages { get; }
    public int FirstIndex { get; }
    public int LastIndex { get; }
    public string StatusText { get; }
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;

    private LibraryPage(
        IReadOnlyList<Document> items,
        int pageNumber,
        int pageSize,
        int totalCount,
        int totalPages,
        int firstIndex,
        int lastIndex
    )
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = totalPages;
        FirstIndex = firstIndex;
        LastIndex = lastIndex;
        StatusText =
            totalCount == 0
                ? "Showing 0 of 0 entries"
                : $"Showing {firstIndex}–{lastIndex} of {totalCount} entries";
    }

    /// <summary>
    /// Slices <paramref name="all"/> (server order preserved) into the requested
    /// 1-based page. Out-of-range pages clamp to the last valid page; an empty
    /// list yields the zero state. Page size must be ≥ 1.
    /// </summary>
    public static LibraryPage Create(IReadOnlyList<Document> all, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(all);
        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var total = all.Count;
        var totalPages = Math.Max(1, (total + pageSize - 1) / pageSize);
        var page = Math.Clamp(pageNumber, 1, totalPages);

        if (total == 0)
        {
            return new LibraryPage(Array.Empty<Document>(), 1, pageSize, 0, 1, 0, 0);
        }

        var start = (page - 1) * pageSize;
        var count = Math.Min(pageSize, total - start);
        var items = new List<Document>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(all[start + i]);
        }

        return new LibraryPage(items, page, pageSize, total, totalPages, start + 1, start + count);
    }
}
