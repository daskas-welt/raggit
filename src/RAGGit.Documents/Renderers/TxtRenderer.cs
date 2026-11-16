using System;
using System.Text;
using RAGGit.Core;
using RAGGit.Core.Models;

namespace RAGGit.Documents.Renderers;

/// <summary>UTF-8 plain-text output with a numbered sources appendix.</summary>
public sealed class TxtRenderer : IDocumentRenderer
{
    public DocumentOutputFormat Format => DocumentOutputFormat.Txt;

    public GeneratedDocument Render(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var title = DocumentFileName.Title(detail.Prompt);
        var labels = DocumentLabels.For(TextScript.IsGreek(detail.Prompt));
        var map = DocumentCitationMapper.Map(detail);

        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine(map.Body);
        builder.AppendLine();
        builder.AppendLine($"{labels.Sources}:");

        foreach (var source in map.Sources)
        {
            builder.AppendLine(
                $"[{source.Index}] {source.DocumentName ?? labels.Document} "
                    + $"(ordinal {source.Ordinal}, chunk {source.ChunkId})"
            );
            builder.AppendLine(source.Excerpt);
            builder.AppendLine();
        }

        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
            builder.ToString()
        );

        return new GeneratedDocument
        {
            Format = DocumentOutputFormat.Txt,
            FileName = DocumentFileName.Create(detail.Prompt, DocumentOutputFormat.Txt),
            ContentType = DocumentOutputFormat.Txt.ContentType(),
            Bytes = bytes,
            Title = title,
            Sources = map.Sources,
        };
    }
}
