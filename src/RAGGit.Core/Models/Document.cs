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
    Txt,
    Md,
}

/// <summary>
/// Document processing status.
/// </summary>
public enum DocumentStatus
{
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
        ErrorMessage = "Unsupported MIME type. Allowed: pdf, docx, txt, md."
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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
}
