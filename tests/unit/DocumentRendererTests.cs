using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using RAGGit.Documents.Renderers;
using UglyToad.PdfPig;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US1: each renderer produces a real file whose content is the persisted
/// grounded answer, with the same sources, in the question's language.
/// </summary>
public sealed class DocumentRendererTests
{
    [Fact]
    public void TxtRenderer_WritesTitleAnswerAndSources()
    {
        var detail = DocumentTestData.Detail();

        var document = new TxtRenderer().Render(detail);
        var text = Encoding.UTF8.GetString(document.Bytes);

        document.Format.Should().Be(DocumentOutputFormat.Txt);
        document.FileName.Should().EndWith(".txt");
        text.Should().Contain(DocumentFileName.Title(detail.Prompt));
        text.Should().Contain("Maria Papadopoulou");
        text.Should().Contain("Sources");
        text.Should().Contain("[1]");
        text.Should().Contain(detail.Citations[0].ChunkId.ToString());
        text.Should().NotContain(detail.Citations[0].ChunkId.ToString() + "]");
    }

    [Fact]
    public void TxtRenderer_UsesGreekLabels()
    {
        var text = Encoding.UTF8.GetString(
            new TxtRenderer().Render(DocumentTestData.GreekDetail()).Bytes
        );

        text.Should().Contain("Πηγές");
        text.Should().Contain("Μαρία");
    }

    [Fact]
    public void DocxRenderer_ProducesAReadableDocument()
    {
        var detail = DocumentTestData.Detail();

        var document = new DocxRenderer().Render(detail);
        using var stream = new MemoryStream(document.Bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        var text = word.MainDocumentPart!.Document.Body!.InnerText;

        document.Format.Should().Be(DocumentOutputFormat.Docx);
        document.FileName.Should().EndWith(".docx");
        text.Should().Contain("Maria Papadopoulou");
        text.Should().Contain("Sources");
        text.Should().Contain(detail.Citations[0].ChunkId.ToString());
    }

    [Fact]
    public void PdfRenderer_ProducesAReadablePdfWithGreek()
    {
        var detail = DocumentTestData.GreekDetail();

        var document = new PdfRenderer().Render(detail);
        using var pdf = PdfDocument.Open(new MemoryStream(document.Bytes));
        var text = pdf.GetPage(1).Text;

        document.Format.Should().Be(DocumentOutputFormat.Pdf);
        document.FileName.Should().EndWith(".pdf");
        text.Should().Contain("Μαρία");
        text.Should().Contain("Πηγές");
    }

    [Fact]
    public void TxtRenderer_RendersSourceFreeDraftWithNotice()
    {
        var draft = SourceFreeDraft();

        var document = new TxtRenderer().Render(draft);
        var text = Encoding.UTF8.GetString(document.Bytes);

        text.Should().Contain(DocumentFileName.Title(draft.Description));
        text.Should().Contain("Welcome to the team.");
        text.Should().Contain("No RAGGit sources were used");
        document.Sources.Should().BeEmpty();
    }

    [Fact]
    public void DocxRenderer_RendersSourceFreeDraftWithNotice()
    {
        var draft = SourceFreeDraft();

        var document = new DocxRenderer().Render(draft);
        using var stream = new MemoryStream(document.Bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        var text = word.MainDocumentPart!.Document.Body!.InnerText;

        text.Should().Contain("Welcome to the team.");
        text.Should().Contain("No RAGGit sources were used");
        document.Sources.Should().BeEmpty();
    }

    [Fact]
    public void PdfRenderer_RendersSourceFreeDraftWithNotice()
    {
        var draft = SourceFreeDraft();

        var document = new PdfRenderer().Render(draft);
        using var pdf = PdfDocument.Open(new MemoryStream(document.Bytes));
        var text = pdf.GetPage(1).Text;

        text.Should().Contain("Welcome to the team.");
        text.Should().Contain("No RAGGit sources were used");
        document.Sources.Should().BeEmpty();
    }

    private static DocumentDraft SourceFreeDraft() =>
        new()
        {
            Description = "Draft a welcome note for a new team member.",
            Body = "Welcome to the team.",
        };

    [Fact]
    public void Renderers_UseTheSameSuggestedFileNameAndSources()
    {
        var detail = DocumentTestData.Detail();

        var txt = new TxtRenderer().Render(detail);
        var docx = new DocxRenderer().Render(detail);
        var pdf = new PdfRenderer().Render(detail);

        txt.FileName.Should().Be(docx.FileName.Replace(".docx", ".txt"));
        txt.Sources.Should().HaveCount(detail.Citations.Count);
        docx.Sources.Should().HaveCount(detail.Citations.Count);
        pdf.Sources.Should().HaveCount(detail.Citations.Count);
    }
}
