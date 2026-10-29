using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// A PDF that parses but carries no text layer is a scan, not a corrupt file.
/// It must fail as "no extractable content" so the library shows the real
/// cause instead of "corrupted pdf".
/// </summary>
public sealed class PdfTextExtractionTests
{
    [Fact]
    public async Task ExtractTextAsync_PdfWithoutTextLayer_ThrowsNoExtractableContent()
    {
        using var stream = new MemoryStream(BuildPdfWithoutText());

        var act = () => Chunker.ExtractTextAsync(stream, DocumentMimeType.Pdf);

        await act.Should().ThrowAsync<NoExtractableContentException>();
    }

    [Fact]
    public async Task ExtractTextAsync_PdfWithTwoColumnForm_PairsEachLabelWithItsOwnValue()
    {
        using var stream = new MemoryStream(BuildTwoColumnFormPdf());

        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Pdf);

        var lines = text.Split(
            ['\n', '\r'],
            System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries
        );
        var afmLine = lines.Single(line => line.Contains("AFM"));
        afmLine.Should().Contain("113220596");
        afmLine.Should().NotContain("AMKA");
        afmLine.Should().NotContain("26057804754");

        var amkaLine = lines.Single(line => line.Contains("AMKA"));
        amkaLine.Should().Contain("26057804754");
        amkaLine.Should().NotContain("AFM");
        amkaLine.Should().NotContain("113220596");
    }

    [Fact]
    public async Task ExtractTextAsync_PdfWithOutOfOrderContentStream_ReadsTopToBottom()
    {
        using var stream = new MemoryStream(BuildOutOfOrderProsePdf());

        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Pdf);

        var lines = text.Split(
            ['\n', '\r'],
            System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries
        );
        lines.Should().HaveCount(2);
        lines[0].Should().Contain("First line");
        lines[1].Should().Contain("Second line");
    }

    [Fact]
    public async Task ExtractTextAsync_TwoColumnProse_JoinsBothColumnsPerBaseline()
    {
        // Known limitation, pinned so a change is deliberate: BuildPageText
        // groups by baseline and does not detect column gutters, so a
        // two-column page emits one line per baseline containing both
        // columns. The form test above relies on the same join.
        using var stream = new MemoryStream(BuildTwoColumnProsePdf());

        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Pdf);

        var lines = text.Split(
            ['\n', '\r'],
            System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries
        );
        lines
            .Should()
            .Contain(line =>
                line.Contains("Left column first.") && line.Contains("Right column first.")
            );
        lines
            .Should()
            .Contain(line =>
                line.Contains("Left column second.") && line.Contains("Right column second.")
            );
    }

    private static byte[] BuildTwoColumnProsePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        // Two columns sharing baselines, as a typical two-column book page.
        page.AddText("Left column first.", 12, new PdfPoint(50, 700), font);
        page.AddText("Right column first.", 12, new PdfPoint(320, 700), font);
        page.AddText("Left column second.", 12, new PdfPoint(50, 680), font);
        page.AddText("Right column second.", 12, new PdfPoint(320, 680), font);
        return builder.Build();
    }

    private static byte[] BuildOutOfOrderProsePdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        // Visual order is First then Second; the content stream is the reverse.
        page.AddText("Second line follows.", 12, new PdfPoint(50, 680), font);
        page.AddText("First line of prose.", 12, new PdfPoint(50, 700), font);
        return builder.Build();
    }

    private static byte[] BuildTwoColumnFormPdf()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        // The content stream is column-major: every label is written before
        // every value, while the values sit to the right of their labels.
        page.AddText("AFM", 12, new PdfPoint(50, 700), font);
        page.AddText("AMKA", 12, new PdfPoint(50, 680), font);
        page.AddText("113220596", 12, new PdfPoint(300, 700), font);
        page.AddText("26057804754", 12, new PdfPoint(300, 680), font);
        return builder.Build();
    }

    private static byte[] BuildPdfWithoutText()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(612, 792);
        return builder.Build();
    }
}
