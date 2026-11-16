using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033: the request wording decides whether a document is produced. Detection
/// must be conservative so ordinary questions are never turned into files.
/// </summary>
public sealed class DocumentDetectionTests
{
    [Theory]
    [InlineData("Create a Word document with the employment history.")]
    [InlineData("Please make a .docx file for this.")]
    [InlineData("Δημιούργησε ένα έγγραφο Word με το ιστορικό.")]
    public void DetectsWord(string prompt)
    {
        var result = DocumentRequestDetector.Detect(prompt);

        result.Format.Should().Be(DocumentOutputFormat.Docx);
        result.IsAmbiguous.Should().BeFalse();
        result.UnsupportedFormat.Should().BeNull();
    }

    [Theory]
    [InlineData("Create an Excel spreadsheet listing the employees.")]
    [InlineData("Put it in Excel.")]
    [InlineData("Export this as an xlsx file.")]
    [InlineData("Φτιάξε ένα υπολογιστικό φύλλο με τους υπαλλήλους.")]
    public void DetectsExcel(string prompt)
    {
        DocumentRequestDetector.Detect(prompt).Format.Should().Be(DocumentOutputFormat.Xlsx);
    }

    [Theory]
    [InlineData("Create a PDF report.")]
    [InlineData("Save this as a PDF.")]
    [InlineData("Δημιούργησε ένα PDF.")]
    public void DetectsPdf(string prompt)
    {
        DocumentRequestDetector.Detect(prompt).Format.Should().Be(DocumentOutputFormat.Pdf);
    }

    [Theory]
    [InlineData("Create a plain text file.")]
    [InlineData("Save it as text.")]
    [InlineData("Write a txt version.")]
    [InlineData("Γράψε ένα κείμενο με την περίληψη.")]
    public void DetectsText(string prompt)
    {
        DocumentRequestDetector.Detect(prompt).Format.Should().Be(DocumentOutputFormat.Txt);
    }

    [Theory]
    [InlineData("What is the refund policy?")]
    [InlineData("What does the text say about the refund?")]
    [InlineData("Create a document about the policy.")]
    [InlineData("Make a report.")]
    public void NoFormatOrNoIntent_ProducesNothing(string prompt)
    {
        var result = DocumentRequestDetector.Detect(prompt);

        result.Format.Should().BeNull();
        result.UnsupportedFormat.Should().BeNull();
        result.IsAmbiguous.Should().BeFalse();
    }

    [Theory]
    [InlineData("Create a PowerPoint presentation.")]
    [InlineData("Export this as a pptx.")]
    [InlineData("Make an image of the summary.")]
    [InlineData("Draft an email about the policy.")]
    public void UnsupportedFormats_AreReported(string prompt)
    {
        var result = DocumentRequestDetector.Detect(prompt);

        result.Format.Should().BeNull();
        result.UnsupportedFormat.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MultipleSupportedFormats_AreAmbiguous()
    {
        var result = DocumentRequestDetector.Detect("Create a Word and PDF version.");

        result.Format.Should().BeNull();
        result.IsAmbiguous.Should().BeTrue();
        result.UnsupportedFormat.Should().BeNull();
    }
}
