namespace RAGGit.Core.Models;

/// <summary>
/// Request body for <c>POST /api/queries/{id}/documents</c> (033). The format is
/// a plain string so invalid values stay on the controller's JSON error path
/// instead of the framework's generic model-state shape.
/// </summary>
public sealed class DocumentRequest
{
    /// <summary>One of <c>docx</c>, <c>xlsx</c>, <c>pdf</c> or <c>txt</c>.</summary>
    public string Format { get; set; } = string.Empty;
}

/// <summary>
/// Result of conservatively reading a prompt for a document request. A format
/// is set only when the prompt names exactly one supported format and contains
/// a document-creation intent; unsupported/ambiguous requests never produce a
/// file automatically.
/// </summary>
public sealed class DocumentFormatDetection
{
    public DocumentOutputFormat? Format { get; init; }

    /// <summary>Canonical name of a known unsupported format (e.g. PowerPoint).</summary>
    public string? UnsupportedFormat { get; init; }

    public bool IsAmbiguous { get; init; }
}
