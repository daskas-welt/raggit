using System;
using System.ComponentModel.DataAnnotations;

namespace RAGGit.Core.Models;

/// <summary>
/// Supported document MIME types per FR-010.
/// </summary>
public enum DocumentMimeType
{
    Pdf,
    Docx,
    Xlsx,
    Txt,
    Md,
}

/// <summary>
/// Document processing status.
/// </summary>
public enum DocumentStatus
{
    Uploading,
    Queued,
    Indexing,
    Ready,
    Failed,
}

/// <summary>
/// A file uploaded to the single-tenant library.
/// </summary>
public sealed class Document
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Filename is required.")]
    public string Filename { get; set; } = string.Empty;

    [Required(ErrorMessage = "MIME type is required.")]
    [EnumDataType(
        typeof(DocumentMimeType),
        ErrorMessage = "Unsupported MIME type. Allowed: pdf, docx, xlsx, txt."
    )]
    public DocumentMimeType Mime { get; set; }

    [Range(
        0,
        DocumentValidation.MaxFileSizeBytes,
        ErrorMessage = "File size must not exceed 100MB."
    )]
    public long Size { get; set; }

    [Required(ErrorMessage = "Hash is required.")]
    public string Hash { get; set; } = string.Empty;

    [Required]
    [EnumDataType(typeof(DocumentStatus))]
    public DocumentStatus Status { get; set; }

    [Required(ErrorMessage = "CreatedBy is required.")]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Uploader display name captured at upload time. Null for legacy rows —
    /// readers fall back to <see cref="CreatedBy"/>. Display only, never used
    /// for auth or isolation.
    /// </summary>
    public string? CreatedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Why background processing last failed, set together with
    /// <see cref="DocumentStatus.Failed"/> and cleared when the document is
    /// (re)staged or indexed successfully. Holds a short, user-safe sentence,
    /// never an exception type or stack trace; null whenever the document is
    /// not in a failed state.
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Returns the content-type string for this document's MIME type.
    /// </summary>
    public string GetContentType() => Mime.GetContentType();
}

/// <summary>
/// MIME-type helpers shared between models, converters, and controllers.
/// </summary>
public static class DocumentMimeTypeExtensions
{
    public static string GetContentType(this DocumentMimeType mime) =>
        mime switch
        {
            DocumentMimeType.Pdf => "application/pdf",
            DocumentMimeType.Docx =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DocumentMimeType.Xlsx =>
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            DocumentMimeType.Txt => "text/plain",
            DocumentMimeType.Md => "text/markdown",
            _ => "application/octet-stream",
        };
}

/// <summary>
/// Shared document validation constants.
/// </summary>
public static class DocumentValidation
{
    public const long MaxFileSizeBytes = 100L * 1024 * 1024;
    public const int MaxSpreadsheetCells = 100_000;

    /// <summary>
    /// Extensions accepted for new uploads (018-allowed-upload-types).
    /// Single source of truth shared by the client queue gate and the
    /// workstation intake gate. Case-insensitive; entries include the dot.
    /// `Md` remains a valid stored <see cref="DocumentMimeType"/> for
    /// pre-existing rows but is no longer accepted for new uploads.
    /// </summary>
    public static readonly System.Collections.Generic.IReadOnlySet<string> AllowedExtensions =
        new System.Collections.Generic.HashSet<string>(
            new[] { ".pdf", ".docx", ".xlsx", ".txt" },
            System.StringComparer.OrdinalIgnoreCase
        );

    /// <summary>
    /// Human-readable allow-list label for dialogs and messages.
    /// </summary>
    public const string SupportedTypesLabel = "PDF, DOCX, XLSX, TXT";
}
