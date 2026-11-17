namespace RAGGit.Core.Models;

/// <summary>
/// Request for one new document composed from a description (034). The format
/// is an explicit choice; the server never substitutes another format.
/// </summary>
public sealed class DocumentCreationRequest
{
    /// <summary>Fixed first-version input bound; not operator-tunable.</summary>
    public const int MaxDescriptionLength = 10_000;

    public string Description { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public bool TryGetFormat(out DocumentOutputFormat format) =>
        DocumentOutputFormatExtensions.TryParse(Format, out format);
}
