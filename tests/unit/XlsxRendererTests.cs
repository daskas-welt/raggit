using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using RAGGit.Documents.Renderers;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US2: the workbook is a real table — header row, one row per record,
/// empty absent values, typed date/number cells and a sources sheet.
/// </summary>
public sealed class XlsxRendererTests
{
    [Fact]
    public void Workbook_HasHeaderRowsTypedCellsAndSources()
    {
        var document = new XlsxRenderer().Render(DocumentTestData.RecordDetail());

        document.Format.Should().Be(DocumentOutputFormat.Xlsx);
        document.FileName.Should().EndWith(".xlsx");
        document.RowCount.Should().Be(2);

        using var stream = new MemoryStream(document.Bytes);
        using var workbook = SpreadsheetDocument.Open(stream, false);
        var data = workbook.WorkbookPart!.WorksheetParts.First();
        var rows = data.Worksheet.Descendants<Row>().ToList();

        rows.Should().HaveCountGreaterThanOrEqualTo(4);
        var headers = rows[1].Elements<Cell>().Select(cell => cell.CellValue!.Text).ToList();
        headers.Should().ContainInOrder("Name", "Role", "Start date", "Missing field", "Sources");

        var firstRecord = rows[2].Elements<Cell>().ToList();
        firstRecord[0].CellValue!.Text.Should().Be("Maria Papadopoulou");
        firstRecord[2].DataType!.Value.Should().Be(CellValues.Number);
        firstRecord[2].StyleIndex!.Value.Should().Be(1);
        firstRecord[3].CellValue!.Text.Should().BeEmpty();

        var sources = workbook.WorkbookPart.WorksheetParts.Last();
        sources
            .Worksheet.Descendants<Row>()
            .First()
            .Elements<Cell>()
            .Select(cell => cell.CellValue!.Text)
            .Should()
            .ContainInOrder("Index", "Document", "Ordinal", "Chunk", "Excerpt");
    }

    [Fact]
    public void NumbersAndPercentages_UseNumericCells()
    {
        var detail = DocumentTestData.Detail(
            answer: "Amount: €1,234.50\nRate: 15%",
            prompt: "Create an Excel spreadsheet with the totals."
        );

        var document = new XlsxRenderer().Render(detail);
        using var stream = new MemoryStream(document.Bytes);
        using var workbook = SpreadsheetDocument.Open(stream, false);
        var rows = workbook
            .WorkbookPart!.WorksheetParts.First()
            .Worksheet.Descendants<Row>()
            .ToList();

        var record = rows[2].Elements<Cell>().ToList();
        record[0].DataType!.Value.Should().Be(CellValues.Number);
        record[0].CellValue!.Text.Should().Be("1234.5");
        record[1].DataType!.Value.Should().Be(CellValues.Number);
        record[1].StyleIndex!.Value.Should().Be(3);
        record[1].CellValue!.Text.Should().Be("0.15");
    }

    [Fact]
    public void ParenthesisedAndEuropeanAmounts_AreNumericCells()
    {
        var detail = DocumentTestData.Detail(
            answer: "Amount: (1,234.50)\nOther: 1.234,56",
            prompt: "Create an Excel spreadsheet with the totals."
        );

        var document = new XlsxRenderer().Render(detail);
        using var stream = new MemoryStream(document.Bytes);
        using var workbook = SpreadsheetDocument.Open(stream, false);
        var rows = workbook
            .WorkbookPart!.WorksheetParts.First()
            .Worksheet.Descendants<Row>()
            .ToList();

        var record = rows[2].Elements<Cell>().ToList();
        record[0].DataType!.Value.Should().Be(CellValues.Number);
        record[0].CellValue!.Text.Should().Be("-1234.5");
        record[1].DataType!.Value.Should().Be(CellValues.Number);
        record[1].CellValue!.Text.Should().Be("1234.56");
    }
}
