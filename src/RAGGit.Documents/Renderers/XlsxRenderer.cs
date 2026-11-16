using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RAGGit.Core;
using RAGGit.Core.Models;

namespace RAGGit.Documents.Renderers;

/// <summary>Excel output with a real table and a separate sources sheet.</summary>
public sealed class XlsxRenderer : IDocumentRenderer
{
    public DocumentOutputFormat Format => DocumentOutputFormat.Xlsx;

    public GeneratedDocument Render(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var title = DocumentFileName.Title(detail.Prompt);
        var labels = DocumentLabels.For(TextScript.IsGreek(detail.Prompt));
        var map = DocumentCitationMapper.Map(detail);
        var sections = DocumentTableBuilder.Build(detail);
        var rowCount = sections.Sum(section => section.Rows.Count);

        using var stream = new MemoryStream();
        using (var package = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = package.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = CreateStylesheet();
            stylesPart.Stylesheet.Save();

            var dataPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            dataPart.Worksheet = new Worksheet(sheetData);

            var sourcesPart = workbookPart.AddNewPart<WorksheetPart>();
            var sourcesData = new SheetData();
            sourcesPart.Worksheet = new Worksheet(sourcesData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(dataPart),
                    SheetId = 1,
                    Name = "Document",
                },
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(sourcesPart),
                    SheetId = 2,
                    Name = labels.Sources,
                }
            );

            var rowIndex = 1u;
            sheetData.Append(TextRow(rowIndex++, title));

            foreach (var section in sections)
            {
                if (section.Rows.Count == 0)
                {
                    continue;
                }

                var headers = section.Rows[0].Cells.Select(cell => cell.Header).ToList();
                headers.Add(labels.Sources);
                sheetData.Append(HeaderRow(rowIndex++, headers));

                foreach (var row in section.Rows)
                {
                    var cells = new List<Cell>();
                    for (var column = 0; column < headers.Count - 1; column++)
                    {
                        var cell =
                            column < row.Cells.Count
                                ? row.Cells[column]
                                : new DocumentCell { Header = headers[column] };
                        cells.Add(DataCell(ColumnName(column), rowIndex, cell));
                    }

                    cells.Add(
                        StringCell(
                            ColumnName(headers.Count - 1),
                            rowIndex,
                            string.Join(
                                ", ",
                                row.Cells.SelectMany(cell => cell.SourceIndexes).Distinct()
                            )
                        )
                    );
                    sheetData.Append(new Row(cells) { RowIndex = rowIndex++ });
                }
            }

            var sourceRow = 1u;
            sourcesData.Append(
                HeaderRow(
                    sourceRow++,
                    new List<string> { "Index", labels.Document, "Ordinal", "Chunk", "Excerpt" }
                )
            );
            foreach (var source in map.Sources)
            {
                sourcesData.Append(
                    new Row(
                        StringCell(
                            "A",
                            sourceRow,
                            source.Index.ToString(CultureInfo.InvariantCulture)
                        ),
                        StringCell("B", sourceRow, source.DocumentName ?? labels.Document),
                        StringCell(
                            "C",
                            sourceRow,
                            source.Ordinal.ToString(CultureInfo.InvariantCulture)
                        ),
                        StringCell("D", sourceRow, source.ChunkId.ToString()),
                        StringCell("E", sourceRow, source.Excerpt)
                    )
                    {
                        RowIndex = sourceRow++,
                    }
                );
            }

            workbookPart.Workbook.Save();
        }

        return new GeneratedDocument
        {
            Format = DocumentOutputFormat.Xlsx,
            FileName = DocumentFileName.Create(detail.Prompt, DocumentOutputFormat.Xlsx),
            ContentType = DocumentOutputFormat.Xlsx.ContentType(),
            Bytes = stream.ToArray(),
            Title = title,
            Sources = map.Sources,
            RowCount = rowCount,
        };
    }

    private static Row TextRow(uint rowIndex, string text) =>
        new(StringCell("A", rowIndex, text)) { RowIndex = rowIndex };

    private static Row HeaderRow(uint rowIndex, IReadOnlyList<string> headers) =>
        new(
            headers
                .Select((header, index) => StringCell(ColumnName(index), rowIndex, header))
                .ToList()
        )
        {
            RowIndex = rowIndex,
        };

    private static Cell DataCell(string column, uint rowIndex, DocumentCell cell) =>
        cell.Kind switch
        {
            DocumentCellKind.Date when cell.DateValue is { } date => new Cell
            {
                CellReference = $"{column}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(date.ToOADate().ToString(CultureInfo.InvariantCulture)),
                StyleIndex = 1,
            },
            DocumentCellKind.Number when cell.NumberValue is { } number => new Cell
            {
                CellReference = $"{column}{rowIndex}",
                DataType = CellValues.Number,
                CellValue = new CellValue(number.ToString(CultureInfo.InvariantCulture)),
                StyleIndex = (UInt32Value)(cell.NumberFormat == "0%" ? 3u : 2u),
            },
            _ => StringCell(column, rowIndex, cell.Text),
        };

    private static Cell StringCell(string column, uint rowIndex, string text) =>
        new()
        {
            CellReference = $"{column}{rowIndex}",
            DataType = CellValues.String,
            CellValue = new CellValue(text),
        };

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
        {
            name = (char)('A' + ((value - 1) % 26)) + name;
        }

        return name;
    }

    private static Stylesheet CreateStylesheet() =>
        new(
            new Fonts(new Font()),
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 })
            ),
            new Borders(new Border()),
            new CellStyleFormats(
                new CellFormat
                {
                    NumberFormatId = 0,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                }
            ),
            new CellFormats(
                new CellFormat
                {
                    NumberFormatId = 0,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                },
                new CellFormat
                {
                    NumberFormatId = 14,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                    ApplyNumberFormat = true,
                },
                new CellFormat
                {
                    NumberFormatId = 4,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                    ApplyNumberFormat = true,
                },
                new CellFormat
                {
                    NumberFormatId = 10,
                    FontId = 0,
                    FillId = 0,
                    BorderId = 0,
                    ApplyNumberFormat = true,
                }
            ),
            new CellStyles(
                new CellStyle
                {
                    Name = "Normal",
                    FormatId = 0,
                    BuiltinId = 0,
                }
            )
        );
}
