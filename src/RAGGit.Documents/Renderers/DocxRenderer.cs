using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RAGGit.Core;
using RAGGit.Core.Models;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace RAGGit.Documents.Renderers;

/// <summary>Word output with a title, the grounded answer and a sources appendix.</summary>
public sealed class DocxRenderer : IDocumentRenderer
{
    public DocumentOutputFormat Format => DocumentOutputFormat.Docx;

    public GeneratedDocument Render(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var title = DocumentFileName.Title(detail.Prompt);
        var labels = DocumentLabels.For(TextScript.IsGreek(detail.Prompt));
        var map = DocumentCitationMapper.Map(detail);

        using var stream = new MemoryStream();
        using (
            var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document)
        )
        {
            var main = package.AddMainDocumentPart();
            var body = new Body();

            body.Append(Heading(title));
            foreach (var line in map.Body.Split('\n'))
            {
                body.Append(Paragraph(line));
            }

            body.Append(Heading(labels.Sources));
            foreach (var source in map.Sources)
            {
                body.Append(
                    Paragraph(
                        $"[{source.Index}] {source.DocumentName ?? labels.Document} "
                            + $"(ordinal {source.Ordinal}, chunk {source.ChunkId})"
                    )
                );
                body.Append(Paragraph(source.Excerpt));
            }

            main.Document = new WordDocument(body);
        }

        return new GeneratedDocument
        {
            Format = DocumentOutputFormat.Docx,
            FileName = DocumentFileName.Create(detail.Prompt, DocumentOutputFormat.Docx),
            ContentType = DocumentOutputFormat.Docx.ContentType(),
            Bytes = stream.ToArray(),
            Title = title,
            Sources = map.Sources,
        };
    }

    private static Paragraph Paragraph(string text) =>
        new(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));

    private static Paragraph Heading(string text) =>
        new(
            new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            new Run(
                new RunProperties(new Bold(), new FontSize { Val = "32" }),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
}
