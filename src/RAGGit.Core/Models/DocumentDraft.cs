using System;
using System.Collections.Generic;
using System.Linq;

namespace RAGGit.Core.Models;

/// <summary>
/// Transient content for one new document composed from a description (034).
/// It is never persisted: the API renders it and returns the bytes.
/// </summary>
public sealed class DocumentDraft
{
    public string Description { get; init; } = string.Empty;

    /// <summary>Generated body; library-derived statements carry citation GUIDs.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Validated library sources; empty means no RAGGit sources were used.</summary>
    public IReadOnlyList<Citation> Sources { get; init; } = Array.Empty<Citation>();

    /// <summary>
    /// True only when adapting a persisted 033 answer. That path keeps unknown
    /// citation markers verbatim so existing exports do not change; new drafts
    /// remove them.
    /// </summary>
    public bool PreserveUnmappedCitationMarkers { get; init; }

    /// <summary>Adapts a persisted, grounded answer for the shared renderers.</summary>
    public static DocumentDraft FromQueryDetail(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        return new DocumentDraft
        {
            Description = detail.Prompt,
            Body = detail.Answer,
            PreserveUnmappedCitationMarkers = true,
            Sources = detail
                .Citations.Select(citation => new Citation
                {
                    DocumentId = citation.DocumentId,
                    DocumentName = citation.DocumentName,
                    ChunkId = citation.ChunkId,
                    Text = citation.Text,
                    Ordinal = citation.Ordinal,
                })
                .ToList(),
        };
    }
}
