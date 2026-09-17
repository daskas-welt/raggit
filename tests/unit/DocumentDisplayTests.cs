using FluentAssertions;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 009-library-item-actions US1 (red-first): pure display helpers for the
/// library row columns — type label, size in MB, creator with raw-id fallback.
/// </summary>
public sealed class DocumentDisplayTests
{
    [Theory]
    [InlineData(DocumentMimeType.Pdf, "pdf")]
    [InlineData(DocumentMimeType.Docx, "docx")]
    [InlineData(DocumentMimeType.Xlsx, "xlsx")]
    [InlineData(DocumentMimeType.Txt, "txt")]
    [InlineData(DocumentMimeType.Md, "md")]
    public void MimeLabel_MapsAllSupportedTypes(DocumentMimeType mime, string expected) =>
        DocumentDisplay.MimeLabel(mime).Should().Be(expected);

    [Fact]
    public void MimeLabel_UnknownValue_FallsBackToSafeToken()
    {
        var label = DocumentDisplay.MimeLabel((DocumentMimeType)999);

        label.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(209715, "0.2 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(0, "0.0 MB")]
    [InlineData(10485760, "10.0 MB")]
    public void FormatSizeMb_FormatsOneDecimal(long bytes, string expected) =>
        DocumentDisplay.FormatSizeMb(bytes).Should().Be(expected);

    [Fact]
    public void FormatSizeMb_Negative_ClampsToZero()
    {
        DocumentDisplay.FormatSizeMb(-5).Should().Be("0.0 MB");
    }

    [Fact]
    public void CreatorLabel_PrefersDisplayName()
    {
        DocumentDisplay.CreatorLabel("Ada", "6a9b731a").Should().Be("Ada");
    }

    [Fact]
    public void CreatorLabel_MissingName_FallsBackToRawId()
    {
        DocumentDisplay.CreatorLabel(null, "admin").Should().Be("admin");
        DocumentDisplay.CreatorLabel("  ", "employee").Should().Be("employee");
    }

    [Fact]
    public void CreatorLabel_MissingBoth_FallsBackToUnknown()
    {
        DocumentDisplay.CreatorLabel(null, "").Should().Be("unknown");
    }

    [Fact]
    public void DocumentOverloads_MatchPrimitiveOverloads()
    {
        var document = new Document
        {
            Mime = DocumentMimeType.Pdf,
            Size = 209715,
            CreatedBy = "admin",
            CreatedByName = "Ada",
        };

        DocumentDisplay.MimeLabel(document).Should().Be("pdf");
        DocumentDisplay.FormatSizeMb(document).Should().Be("0.2 MB");
        DocumentDisplay.CreatorLabel(document).Should().Be("Ada");
    }
}
