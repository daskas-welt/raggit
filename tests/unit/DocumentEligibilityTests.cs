using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033: a document is produced only for a grounded, cited answer, and only
/// within the size/row caps that keep the workstation responsive.
/// </summary>
public sealed class DocumentEligibilityTests
{
    [Fact]
    public void Refusal_IsNotEligible()
    {
        DocumentEligibility.IsContentEligible(DocumentTestData.RefusalDetail()).Should().BeFalse();
    }

    [Fact]
    public void AnswerWithoutCitations_IsNotEligible()
    {
        var detail = DocumentTestData.Detail(citations: System.Array.Empty<HistoryCitation>());

        DocumentEligibility.IsContentEligible(detail).Should().BeFalse();
    }

    [Fact]
    public void CitedAnswer_IsEligible()
    {
        DocumentEligibility.IsContentEligible(DocumentTestData.Detail()).Should().BeTrue();
    }

    [Theory]
    [InlineData(DocumentEligibility.MaxDocumentBytes, DocumentEligibility.MaxTableRows, true)]
    [InlineData(DocumentEligibility.MaxDocumentBytes + 1, 0, false)]
    [InlineData(0, DocumentEligibility.MaxTableRows + 1, false)]
    public void Caps_AreEnforced(long byteCount, int rowCount, bool expected)
    {
        DocumentEligibility.IsWithinCaps(byteCount, rowCount).Should().Be(expected);
    }
}
