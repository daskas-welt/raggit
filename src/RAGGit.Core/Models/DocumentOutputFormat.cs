using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// The four document formats an employee can ask for (033 FR-001). Wire values
/// are lowercase, matching the existing <see cref="QueryStage"/> convention.
/// </summary>
public enum DocumentOutputFormat
{
    [JsonStringEnumMemberName("docx")]
    Docx = 0,

    [JsonStringEnumMemberName("xlsx")]
    Xlsx = 1,

    [JsonStringEnumMemberName("pdf")]
    Pdf = 2,

    [JsonStringEnumMemberName("txt")]
    Txt = 3,
}

/// <summary>Format helpers shared by the API, the client and the renderers.</summary>
public static class DocumentOutputFormatExtensions
{
    /// <summary>HTTP content type for the rendered file.</summary>
    public static string ContentType(this DocumentOutputFormat format) =>
        format switch
        {
            DocumentOutputFormat.Docx =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DocumentOutputFormat.Xlsx =>
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            DocumentOutputFormat.Pdf => "application/pdf",
            DocumentOutputFormat.Txt => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };

    /// <summary>Filename extension including the leading dot.</summary>
    public static string Extension(this DocumentOutputFormat format) =>
        format switch
        {
            DocumentOutputFormat.Docx => ".docx",
            DocumentOutputFormat.Xlsx => ".xlsx",
            DocumentOutputFormat.Pdf => ".pdf",
            DocumentOutputFormat.Txt => ".txt",
            _ => ".bin",
        };

    /// <summary>Lowercase wire value used by the document request body.</summary>
    public static string WireValue(this DocumentOutputFormat format) =>
        format switch
        {
            DocumentOutputFormat.Docx => "docx",
            DocumentOutputFormat.Xlsx => "xlsx",
            DocumentOutputFormat.Pdf => "pdf",
            DocumentOutputFormat.Txt => "txt",
            _ => string.Empty,
        };

    /// <summary>Parses a wire value case-insensitively; only the four formats are accepted.</summary>
    public static bool TryParse(string? value, out DocumentOutputFormat format)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "docx":
                format = DocumentOutputFormat.Docx;
                return true;
            case "xlsx":
                format = DocumentOutputFormat.Xlsx;
                return true;
            case "pdf":
                format = DocumentOutputFormat.Pdf;
                return true;
            case "txt":
                format = DocumentOutputFormat.Txt;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
