using System;
using System.Collections.Generic;

namespace RAGGit.Core.Models;

/// <summary>
/// Per-person history projections per specs/005-per-person-history/contracts/api.yaml (1.4.0).
/// Read-only views over Queries/Documents already attributed by 004-identity; no new tables.
/// JSON is camelCase via the shared Web defaults (id, promptPreview, ...).
/// </summary>
public sealed class HistoryItem
{
    public Guid Id { get; set; }

    public string PromptPreview { get; set; } = string.Empty;

    public string AnswerPreview { get; set; } = string.Empty;

    public int CitationCount { get; set; }

    public int LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Paginated history envelope: total is the filtered count before paging.
/// </summary>
public sealed class HistoryPage
{
    public List<HistoryItem> Items { get; set; } = new();

    public int Total { get; set; }

    public int Limit { get; set; }

    public int Offset { get; set; }
}

/// <summary>
/// Single citation in a query detail response, ordered by ordinal.
/// </summary>
public sealed class HistoryCitation
{
    public Guid DocumentId { get; set; }

    public Guid ChunkId { get; set; }

    public string Text { get; set; } = string.Empty;

    public int Ordinal { get; set; }
}

/// <summary>
/// Full prompt/answer plus citations for one owned query.
/// </summary>
public sealed class QueryDetail
{
    public Guid Id { get; set; }

    public string Prompt { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public List<HistoryCitation> Citations { get; set; } = new();

    public int LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One document uploaded by the caller (CreatedBy == sub).
/// Status is a string to stay additive over the core DocumentStatus enum.
/// </summary>
public sealed class DocumentMineItem
{
    public Guid Id { get; set; }

    public string Filename { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Paginated own-documents envelope.
/// </summary>
public sealed class DocumentsMinePage
{
    public List<DocumentMineItem> Items { get; set; } = new();

    public int Total { get; set; }

    public int Limit { get; set; }

    public int Offset { get; set; }
}
