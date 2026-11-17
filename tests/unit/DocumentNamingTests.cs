using System;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033: a document gets a readable title and an OS-valid suggested filename,
/// and its citations become stable numbered references with a sources appendix.
/// </summary>
public sealed class DocumentNamingTests
{
    [Fact]
    public void Title_DropsCommandAndFormatWords()
    {
        var title = DocumentFileName.Title(
            "Create a Word document about the employment history of Μαρία Παπαδοπούλου."
        );

        title.Should().Contain("Μαρία Παπαδοπούλου");
        title.Should().NotContain("Create");
        title.Should().NotContain("Word");
    }

    [Fact]
    public void FileName_IsValidAndHasTheFormatExtension()
    {
        var fileName = DocumentFileName.Create(
            "Create a Word document: employment history / Μαρία Παπαδοπούλου?",
            DocumentOutputFormat.Docx
        );

        fileName.Should().EndWith(".docx");
        fileName.Should().NotContain("/");
        fileName.Should().NotContain("?");
        fileName.Should().Contain("Μαρία");
    }

    [Fact]
    public void FileName_FallsBackWhenNothingRemains()
    {
        DocumentFileName
            .Create("Create a Word document", DocumentOutputFormat.Docx)
            .Should()
            .Be("document.docx");
    }

    [Fact]
    public void FileName_IsCappedAtEightyCharactersBeforeTheExtension()
    {
        var prompt = "Create a Word document about " + new string('a', 300);

        var fileName = DocumentFileName.Create(prompt, DocumentOutputFormat.Docx);

        fileName.Should().EndWith(".docx");
        fileName[..^5].Length.Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public void Labels_AreLocalized()
    {
        DocumentLabels.For(isGreek: false).Title.Should().Be("Title");
        DocumentLabels.For(isGreek: false).Sources.Should().Be("Sources");
        DocumentLabels.For(isGreek: true).Title.Should().Be("Τίτλος");
        DocumentLabels.For(isGreek: true).Sources.Should().Be("Πηγές");
    }

    [Fact]
    public void PersistedAnswer_KeepsAnUnknownCitationMarkerUnchanged()
    {
        var unknown = Guid.NewGuid();
        var detail = DocumentTestData.Detail(
            answer: $"A claim [{unknown}].",
            citations: Array.Empty<HistoryCitation>()
        );

        DocumentCitationMapper.Map(detail).Body.Should().Contain(unknown.ToString());
    }

    [Fact]
    public void NewDraft_RemovesAnUnknownCitationMarker()
    {
        var unknown = Guid.NewGuid();
        var draft = new DocumentDraft
        {
            Description = "Draft a note.",
            Body = $"A claim [{unknown}].",
        };

        DocumentCitationMapper.Map(draft).Body.Should().NotContain(unknown.ToString());
    }

    [Fact]
    public void CitationMapper_NumbersByFirstAppearanceInTheAnswer()
    {
        var first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var detail = DocumentTestData.Detail(
            answer: $"First claim [{second}], second claim [{first}], again [{second}].",
            citations: new[]
            {
                DocumentTestData.Citation(chunkId: first, ordinal: 1),
                DocumentTestData.Citation(chunkId: second, ordinal: 2),
            }
        );

        var map = DocumentCitationMapper.Map(detail);

        map.Body.Should().Contain("[1]");
        map.Body.Should().Contain("[2]");
        map.Body.Should().NotContain(second.ToString());
        map.Sources.Should().HaveCount(2);
        map.Sources[0].ChunkId.Should().Be(second);
        map.Sources[0].Index.Should().Be(1);
        map.Sources[1].ChunkId.Should().Be(first);
        map.Sources[1].Index.Should().Be(2);
    }
}
