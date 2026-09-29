using System;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>
/// Semantic tone for a document status, independent of any UI framework so the
/// mapping is unit-testable on plain net8.0 (006-client-architecture US4/FR-008).
/// </summary>
public enum StatusTone
{
    Neutral,
    Positive,
    InProgress,
    Error,
}

/// <summary>
/// Single source of truth for how a document status is presented (label + tone).
/// The WinUI <c>StatusChip</c> maps <see cref="StatusTone"/> to colors; every
/// screen shows the same label/tone for the same status.
/// </summary>
public static class DocumentStatusPresentation
{
    public static StatusTone ToneFor(DocumentStatus status) =>
        status switch
        {
            DocumentStatus.Ready => StatusTone.Positive,
            DocumentStatus.Uploading => StatusTone.InProgress,
            DocumentStatus.Queued => StatusTone.InProgress,
            DocumentStatus.Indexing => StatusTone.InProgress,
            DocumentStatus.Failed => StatusTone.Error,
            _ => StatusTone.Neutral,
        };

    public static StatusTone ToneFor(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return StatusTone.Neutral;
        }

        if (Enum.TryParse<DocumentStatus>(status.Trim(), ignoreCase: true, out var parsed))
        {
            return ToneFor(parsed);
        }

        var s = status.ToLowerInvariant();
        if (Contains(s, "ready", "done", "complete", "active"))
        {
            return StatusTone.Positive;
        }

        if (Contains(s, "index", "process", "pending", "upload"))
        {
            return StatusTone.InProgress;
        }

        if (Contains(s, "fail", "error", "locked"))
        {
            return StatusTone.Error;
        }

        return StatusTone.Neutral;
    }

    public static string LabelFor(DocumentStatus status) =>
        status switch
        {
            DocumentStatus.Ready => "Ready",
            DocumentStatus.Uploading => "Uploading",
            DocumentStatus.Queued => "Queued",
            DocumentStatus.Indexing => "Indexing",
            DocumentStatus.Failed => "Failed",
            _ => status.ToString(),
        };

    public static string LabelFor(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Unknown";
        }

        return Enum.TryParse<DocumentStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? LabelFor(parsed)
            : status;
    }

    private static bool Contains(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
