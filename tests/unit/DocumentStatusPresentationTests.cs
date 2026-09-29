using FluentAssertions;
using RAGGit.Client.Core.Models;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US4/FR-008: document status is mapped to one
/// consistent label + tone, shared by every screen (Library enum vs My Docs
/// string produce the same presentation) and unit-testable without the UI shell.
/// </summary>
public sealed class DocumentStatusPresentationTests
{
    [Theory]
    [InlineData("Ready", StatusTone.Positive)]
    [InlineData("ready", StatusTone.Positive)]
    [InlineData("Indexing", StatusTone.InProgress)]
    [InlineData("Uploading", StatusTone.InProgress)]
    [InlineData("Queued", StatusTone.InProgress)]
    [InlineData("processing", StatusTone.InProgress)]
    [InlineData("Failed", StatusTone.Error)]
    [InlineData("mystery", StatusTone.Neutral)]
    public void ToneFor_String_MapsStatuses(string status, StatusTone expected) =>
        DocumentStatusPresentation.ToneFor(status).Should().Be(expected);

    [Fact]
    public void ToneFor_Null_IsNeutral() =>
        DocumentStatusPresentation.ToneFor((string?)null).Should().Be(StatusTone.Neutral);

    [Fact]
    public void ToneFor_Enum_MatchesItsStringForm()
    {
        foreach (
            var status in new[]
            {
                DocumentStatus.Ready,
                DocumentStatus.Uploading,
                DocumentStatus.Queued,
                DocumentStatus.Indexing,
                DocumentStatus.Failed,
            }
        )
        {
            DocumentStatusPresentation
                .ToneFor(status)
                .Should()
                .Be(DocumentStatusPresentation.ToneFor(status.ToString()));
        }
    }

    [Theory]
    [InlineData(DocumentStatus.Ready, "Ready")]
    [InlineData(DocumentStatus.Uploading, "Uploading")]
    [InlineData(DocumentStatus.Queued, "Queued")]
    [InlineData(DocumentStatus.Indexing, "Indexing")]
    [InlineData(DocumentStatus.Failed, "Failed")]
    public void LabelFor_Enum_Normalizes(DocumentStatus status, string expected) =>
        DocumentStatusPresentation.LabelFor(status).Should().Be(expected);

    [Fact]
    public void LabelFor_String_NormalizesKnownAndPreservesUnknown()
    {
        DocumentStatusPresentation.LabelFor("indexing").Should().Be("Indexing");
        DocumentStatusPresentation.LabelFor("WeirdValue").Should().Be("WeirdValue");
        DocumentStatusPresentation.LabelFor((string?)null).Should().Be("Unknown");
    }
}
