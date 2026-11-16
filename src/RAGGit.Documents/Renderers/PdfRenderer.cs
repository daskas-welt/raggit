using System;
using System.IO;
using System.Text;
using RAGGit.Core;
using RAGGit.Core.Models;
using SkiaSharp;

namespace RAGGit.Documents.Renderers;

/// <summary>
/// PDF output through SkiaSharp's PDF backend with the embedded DejaVu font.
/// MIT-licensed and deterministic on Windows and Linux, including Greek text.
/// </summary>
public sealed class PdfRenderer : IDocumentRenderer
{
    private const float PageWidth = 595f; // A4 at 72 dpi
    private const float PageHeight = 842f;
    private const float Margin = 50f;
    private const float ContentWidth = PageWidth - (Margin * 2);
    private const float LineHeight = 15f;

    public DocumentOutputFormat Format => DocumentOutputFormat.Pdf;

    public GeneratedDocument Render(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var title = DocumentFileName.Title(detail.Prompt);
        var labels = DocumentLabels.For(TextScript.IsGreek(detail.Prompt));
        var map = DocumentCitationMapper.Map(detail);

        using var stream = new MemoryStream();
        using (var document = SKDocument.CreatePdf(stream))
        {
            using var regularFace = LoadTypeface("DejaVuSans");
            using var boldFace = LoadTypeface("DejaVuSans-Bold");
            using var bodyFont = new SKFont(regularFace, 10.5f);
            using var titleFont = new SKFont(boldFace, 18f);
            using var headingFont = new SKFont(boldFace, 13f);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

            var writer = new PdfPageWriter(document, paint);
            writer.WriteWrapped(title, titleFont, before: 0, after: 10);
            writer.WriteWrapped(map.Body, bodyFont, before: 0, after: 8);
            writer.WriteWrapped(labels.Sources, headingFont, before: 10, after: 4);

            foreach (var source in map.Sources)
            {
                writer.WriteWrapped(
                    $"[{source.Index}] {source.DocumentName ?? labels.Document} "
                        + $"(ordinal {source.Ordinal}, chunk {source.ChunkId})",
                    bodyFont,
                    before: 0,
                    after: 0
                );
                writer.WriteWrapped(source.Excerpt, bodyFont, before: 0, after: 6);
            }

            writer.Finish();
            document.Close();
        }

        return new GeneratedDocument
        {
            Format = DocumentOutputFormat.Pdf,
            FileName = DocumentFileName.Create(detail.Prompt, DocumentOutputFormat.Pdf),
            ContentType = DocumentOutputFormat.Pdf.ContentType(),
            Bytes = stream.ToArray(),
            Title = title,
            Sources = map.Sources,
        };
    }

    private static SKTypeface LoadTypeface(string faceName)
    {
        var assembly = typeof(PdfRenderer).Assembly;
        var resourceName = $"RAGGit.Documents.Assets.{faceName}.ttf";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded font '{resourceName}' was not found.");
        }

        return SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException($"Font '{resourceName}' could not be loaded.");
    }

    private sealed class PdfPageWriter
    {
        private readonly SKDocument _document;
        private readonly SKPaint _paint;
        private SKCanvas _canvas;
        private float _y = Margin;

        public PdfPageWriter(SKDocument document, SKPaint paint)
        {
            _document = document;
            _paint = paint;
            _canvas = document.BeginPage(PageWidth, PageHeight);
        }

        public void WriteWrapped(string text, SKFont font, float before, float after)
        {
            _y += before;

            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    Advance(LineHeight);
                    continue;
                }

                var line = new StringBuilder();
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : $"{line} {word}";
                    if (line.Length > 0 && font.MeasureText(candidate) > ContentWidth)
                    {
                        Draw(line.ToString(), font);
                        line.Clear();
                        line.Append(word);
                    }
                    else
                    {
                        line.Clear();
                        line.Append(candidate);
                    }
                }

                if (line.Length > 0)
                {
                    Draw(line.ToString(), font);
                }
            }

            _y += after;
        }

        public void Finish()
        {
            _document.EndPage();
        }

        private void Draw(string text, SKFont font)
        {
            Advance(LineHeight);
            _canvas.DrawText(text, Margin, _y, SKTextAlign.Left, font, _paint);
        }

        private void Advance(float amount)
        {
            if (_y + amount > PageHeight - Margin)
            {
                _document.EndPage();
                _canvas = _document.BeginPage(PageWidth, PageHeight);
                _y = Margin;
            }

            _y += amount;
        }
    }
}
