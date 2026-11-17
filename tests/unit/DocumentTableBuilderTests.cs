using System.Linq;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US2: repeated label/value blocks become a record table; prose becomes
/// titled sections; absent values stay empty and recognized dates/amounts are
/// typed without guessing.
/// </summary>
public sealed class DocumentTableBuilderTests
{
    [Fact]
    public void RepeatedBlocks_BecomeOneRecordTable()
    {
        var sections = DocumentTableBuilder.Build(DocumentTestData.RecordDetail());

        sections.Should().ContainSingle();
        var section = sections[0];
        section.IsRecordShape.Should().BeTrue();
        section.Rows.Should().HaveCount(2);
        section
            .Rows[0]
            .Cells.Select(cell => cell.Header)
            .Should()
            .ContainInOrder("Name", "Role", "Start date", "Missing field");
        section.Rows[0].Cells[0].Text.Should().Be("Maria Papadopoulou");
        section.Rows[0].Cells[2].Kind.Should().Be(DocumentCellKind.Date);
        section.Rows[0].Cells[2].DateValue.Should().Be(new System.DateTime(2019, 9, 1));
        section.Rows[1].Cells[3].Text.Should().BeEmpty();
    }

    [Fact]
    public void Prose_BecomesSectionRows()
    {
        var sections = DocumentTableBuilder.Build(
            DocumentTestData.Detail(
                answer: "The policy allows returns.\nRefunds are paid within 30 days.",
                prompt: "Create an Excel spreadsheet about the policy."
            )
        );

        sections.Should().ContainSingle();
        sections[0].IsRecordShape.Should().BeFalse();
        sections[0].Rows.Should().NotBeEmpty();
        sections[0].Rows[0].Cells[0].Text.Should().Contain("returns");
    }

    [Fact]
    public void MixedRecordAndProse_KeepBoth()
    {
        var draft = new DocumentDraft
        {
            Description = "Create a spreadsheet with employee records and a note.",
            Body =
                "Name: Maria Papadopoulou\nRole: Analyst\n\nNote: The records above come from the current library.",
        };

        var sections = DocumentTableBuilder.Build(draft);

        sections.Should().HaveCount(2);
        sections[0].IsRecordShape.Should().BeTrue();
        sections[0].Rows.Should().ContainSingle();
        sections[1].IsRecordShape.Should().BeFalse();
        sections[1].Rows[0].Cells[0].Text.Should().Contain("current library");
    }

    [Fact]
    public void SourceFreeMarkers_DoNotBecomeSourceIndexes()
    {
        var draft = new DocumentDraft
        {
            Description = "Create a note.",
            Body = "Only relevant records were used [1].",
        };

        var cells = DocumentTableBuilder.Build(draft)[0].Rows[0].Cells;

        cells.SelectMany(cell => cell.SourceIndexes).Should().BeEmpty();
    }

    [Fact]
    public void DatesAndAmounts_AreTyped()
    {
        var detail = DocumentTestData.Detail(
            answer: "Date: 2024-01-31\nAmount: €1,234.50\nRate: 15%",
            prompt: "Create an Excel spreadsheet with the totals."
        );

        var cells = DocumentTableBuilder.Build(detail)[0].Rows[0].Cells;

        cells[0].Kind.Should().Be(DocumentCellKind.Date);
        cells[1].Kind.Should().Be(DocumentCellKind.Number);
        cells[1].NumberValue.Should().Be(1234.50);
        cells[2].Kind.Should().Be(DocumentCellKind.Number);
        cells[2].NumberValue.Should().Be(0.15);
        cells[2].NumberFormat.Should().Be("0%");
    }

    [Fact]
    public void ParenthesisedNegatives_AreTyped()
    {
        var detail = DocumentTestData.Detail(
            answer: "Amount: (1,234.50)\nRate: (15%)",
            prompt: "Create an Excel spreadsheet with the totals."
        );

        var cells = DocumentTableBuilder.Build(detail)[0].Rows[0].Cells;

        cells[0].Kind.Should().Be(DocumentCellKind.Number);
        cells[0].NumberValue.Should().Be(-1234.50);
        cells[1].Kind.Should().Be(DocumentCellKind.Number);
        cells[1].NumberValue.Should().Be(-0.15);
        cells[1].NumberFormat.Should().Be("0%");
    }

    [Fact]
    public void EuropeanAmounts_AreTypedWithoutMisreading()
    {
        var detail = DocumentTestData.Detail(
            answer: "Amount: €1.234,56\nOther: 1.234,56",
            prompt: "Create an Excel spreadsheet with the totals."
        );

        var cells = DocumentTableBuilder.Build(detail)[0].Rows[0].Cells;

        cells[0].Kind.Should().Be(DocumentCellKind.Number);
        cells[0].NumberValue.Should().Be(1234.56);
        cells[1].Kind.Should().Be(DocumentCellKind.Number);
        cells[1].NumberValue.Should().Be(1234.56);
    }
}
