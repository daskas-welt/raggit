using System;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents.Renderers;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US4: the document adds no facts; every source it lists is a persisted
/// citation, in the order the answer first cites it.
/// </summary>
public sealed class DocumentTrustTests
{
    [Fact]
    public void Txt_PreservesTheAnswerAndListsEveryCitedChunk()
    {
        var first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var detail = DocumentTestData.Detail(
            answer: $"First claim [{second}]. Second claim [{first}].",
            citations: new[]
            {
                DocumentTestData.Citation(chunkId: first, text: "first source", ordinal: 1),
                DocumentTestData.Citation(chunkId: second, text: "second source", ordinal: 2),
            }
        );

        var document = new TxtRenderer().Render(detail);
        var text = Encoding.UTF8.GetString(document.Bytes);
        var body = text.Split("Sources:")[0];

        body.Should().Contain("First claim [1]. Second claim [2].");
        body.Should().NotContain(second.ToString());
        body.Should().NotContain(first.ToString());
        text.Should().Contain(second.ToString());
        document.Sources.Select(source => source.ChunkId).Should().ContainInOrder(second, first);
        document
            .Sources.Select(source => source.Excerpt)
            .Should()
            .ContainInOrder("second source", "first source");
    }

    [Fact]
    public void Docx_ContainsOnlyThePersistedSources()
    {
        var detail = DocumentTestData.Detail();
        var document = new DocxRenderer().Render(detail);

        using var stream = new MemoryStream(document.Bytes);
        using var word = WordprocessingDocument.Open(stream, false);
        var text = word.MainDocumentPart!.Document.Body!.InnerText;

        text.Should().Contain(detail.Citations[0].Text);
        text.Should().Contain(detail.Citations[0].ChunkId.ToString());
        document.Sources.Should().HaveCount(detail.Citations.Count);
    }

    [Fact]
    public void SameTemplateRecord_DoesNotAddAnotherPersonsContent()
    {
        var detail = DocumentTestData.Detail(
            answer: "Maria Papadopoulou is an analyst [22222222-2222-2222-2222-222222222222].",
            citations: new[]
            {
                DocumentTestData.Citation(text: "Maria Papadopoulou is an analyst."),
            }
        );

        var document = new TxtRenderer().Render(detail);
        var text = Encoding.UTF8.GetString(document.Bytes);

        text.Should().Contain("Maria Papadopoulou");
        text.Should().NotContain("Giorgos");
        document.Sources.Should().ContainSingle();
    }
}
