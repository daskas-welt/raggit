using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
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

    private static byte[] BuildPdfWithoutText()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(612, 792);
        return builder.Build();
    }
}
