using System;
using System.Collections.Generic;
using System.Linq;
using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>
/// Dispatches one persisted grounded answer to the requested renderer, after
/// the eligibility and size/row-cap checks (033 FR-018/FR-019/FR-025).
/// </summary>
public sealed class DocumentGenerator
{
    private readonly IReadOnlyDictionary<DocumentOutputFormat, IDocumentRenderer> _renderers;

    public DocumentGenerator(IEnumerable<IDocumentRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);

        var map = new Dictionary<DocumentOutputFormat, IDocumentRenderer>();
        foreach (var renderer in renderers)
        {
            map[renderer.Format] = renderer;
        }

        _renderers = map;
    }

    public GeneratedDocument Generate(QueryDetail detail, DocumentOutputFormat format)
    {
        ArgumentNullException.ThrowIfNull(detail);

        if (!DocumentEligibility.IsContentEligible(detail))
        {
            throw new DocumentGenerationException(
                DocumentGenerationError.NotEligible,
                "no document available"
            );
        }

        if (!_renderers.TryGetValue(format, out var renderer))
        {
            throw new DocumentGenerationException(
                DocumentGenerationError.UnsupportedFormat,
                "unsupported format"
            );
        }

        var document = renderer.Render(detail);
        if (!DocumentEligibility.IsWithinCaps(document.ByteCount, document.RowCount))
        {
            throw new DocumentGenerationException(
                DocumentGenerationError.TooLarge,
                "document too large"
            );
        }

        return document;
    }

    /// <summary>The formats this generator can render.</summary>
    public IReadOnlyCollection<DocumentOutputFormat> Formats => _renderers.Keys.ToArray();
}
