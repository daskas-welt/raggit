using System;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>
/// Pure display helpers for library row columns (009). Framework-free and
/// unit-testable; WinUI value converters delegate to these.
/// </summary>
public static class DocumentDisplay
{
    /// <summary>
    /// Short lowercase type label; safe fallback token, never blank.
    /// </summary>
    public static string MimeLabel(DocumentMimeType mime) =>
        mime switch
        {
            DocumentMimeType.Pdf => "pdf",
            DocumentMimeType.Docx => "docx",
            DocumentMimeType.Xlsx => "xlsx",
            DocumentMimeType.Txt => "txt",
            DocumentMimeType.Md => "md",
            _ => "file",
        };

    /// <summary>
    /// Short lowercase type label for a document row.
    /// </summary>
    public static string MimeLabel(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return MimeLabel(document.Mime);
    }

    /// <summary>
    /// Size in megabytes with one decimal (e.g. "0.2 MB"); negatives clamp to zero.
    /// </summary>
    public static string FormatSizeMb(long bytes)
    {
        var clamped = Math.Max(0, bytes);
        return $"{clamped / 1048576.0:0.0} MB";
    }

    /// <summary>
    /// Size in megabytes for a document row.
    /// </summary>
    public static string FormatSizeMb(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return FormatSizeMb(document.Size);
    }

    /// <summary>
    /// Uploader display name when recorded, else the raw creator id, else "unknown".
    /// </summary>
    public static string CreatorLabel(string? displayName, string fallbackId)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackId))
        {
            return fallbackId;
        }

        return "unknown";
    }

    /// <summary>
    /// Creator label for a document row.
    /// </summary>
    public static string CreatorLabel(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return CreatorLabel(document.CreatedByName, document.CreatedBy);
    }
}
