using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Fluent in-memory workbook builder for unit tests. Uses DocumentFormat.OpenXml 3.1.0
/// already referenced — no new NuGet. Returns a MemoryStream positioned at 0.
/// Covers: Hidden/VeryHidden, SharedString vs InlineString, date 1900/1904,
/// merged cells, sparse/wide rows, exact cap boundaries, blank-row sheets.
/// </summary>
public sealed class XlsxWorkbookBuilder
{
    private readonly List<SheetSpec> _sheets = new();
    private bool _use1904DateSystem;

    public XlsxWorkbookBuilder Use1904DateSystem(bool value = true)
    {
        _use1904DateSystem = value;
        return this;
    }

    public SheetBuilder AddSheet(string name, SheetStateValues? state = null)
    {
        var spec = new SheetSpec { Name = name, State = state };
        _sheets.Add(spec);
        return new SheetBuilder(this, spec);
    }

    public MemoryStream Build()
    {
        // Handle hidden-only special case: if all sheets are hidden, keep one visible then patch later
        var needsPatchHiddenOnly =
            _sheets.Count > 0
            && _sheets.All(s =>
                s.State == SheetStateValues.Hidden || s.State == SheetStateValues.VeryHidden
            );
        var patchTargets = needsPatchHiddenOnly ? _sheets.Select(s => s.Name).ToHashSet() : null;

        // Temporarily make first sheet visible for openpyxl-like workaround
        SheetStateValues? savedFirstState = null;
        if (needsPatchHiddenOnly)
        {
            savedFirstState = _sheets[0].State;
            _sheets[0].State = null;
        }

        var stream = new MemoryStream();
        using (
            var document = SpreadsheetDocument.Create(
                stream,
                SpreadsheetDocumentType.Workbook,
                autoSave: true
            )
        )
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            if (_use1904DateSystem)
            {
                workbookPart.Workbook.WorkbookProperties = new WorkbookProperties
                {
                    Date1904 = true,
                };
            }

            workbookPart.Workbook.Sheets = new Sheets();

            // Shared strings table
            var sstPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sstPart.SharedStringTable = new SharedStringTable();
            var sharedStrings = new Dictionary<string, int>();

            int GetSharedStringIndex(string text)
            {
                if (sharedStrings.TryGetValue(text, out var idx))
                    return idx;
                idx = sharedStrings.Count;
                sharedStrings[text] = idx;
                sstPart.SharedStringTable.AppendChild(new SharedStringItem(new Text(text)));
                sstPart.SharedStringTable.Count = (uint)sharedStrings.Count;
                sstPart.SharedStringTable.UniqueCount = (uint)sharedStrings.Count;
                return idx;
            }

            // Styles for date format if needed
            WorkbookStylesPart? stylesPart = null;
            if (_sheets.Any(s => s.Rows.Any(r => r.Cells.Any(c => c.IsDate))))
            {
                stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
                // Minimal stylesheet with date format
                var numberingFormats = new NumberingFormats { Count = 1 };
                numberingFormats.AppendChild(
                    new NumberingFormat { NumberFormatId = 164, FormatCode = "yyyy-mm-dd" }
                );
                var cellFormats = new CellFormats { Count = 2 };
                cellFormats.AppendChild(
                    new CellFormat
                    {
                        NumberFormatId = 0,
                        FontId = 0,
                        FillId = 0,
                        BorderId = 0,
                    }
                );
                cellFormats.AppendChild(
                    new CellFormat
                    {
                        NumberFormatId = 164,
                        FontId = 0,
                        FillId = 0,
                        BorderId = 0,
                        ApplyNumberFormat = true,
                    }
                );
                stylesPart.Stylesheet = new Stylesheet(
                    new Fonts(new Font()),
                    new Fills(new Fill(new PatternFill { PatternType = PatternValues.None })),
                    new Borders(new Border()),
                    new CellStyleFormats(new CellFormat()),
                    numberingFormats,
                    cellFormats
                );
            }

            uint sheetId = 1;
            foreach (var spec in _sheets)
            {
                var wsPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                var worksheet = new Worksheet(sheetData);

                // Handle merged cells
                if (spec.MergedRanges.Count > 0)
                {
                    var mergeCells = new MergeCells();
                    foreach (var range in spec.MergedRanges)
                        mergeCells.AppendChild(new MergeCell { Reference = range });
                    worksheet.AppendChild(mergeCells);
                }

                // Rows
                uint rowIndex = 1;
                foreach (var rowSpec in spec.Rows)
                {
                    // Skip completely empty row spec if configured? We still emit Row if has cells
                    var row = new Row();
                    // Determine column letters
                    for (int colIdx = 0; colIdx < rowSpec.Cells.Count; colIdx++)
                    {
                        var cellSpec = rowSpec.Cells[colIdx];
                        var colLetter = GetColumnLetter(colIdx + 1);
                        var cellRef = $"{colLetter}{rowIndex}";
                        var cell = new Cell { CellReference = cellRef };

                        if (cellSpec.IsBlank)
                        {
                            // Blank cell: emit empty <c> with no <v> (still counts toward cap as per spec: <c> tag presence)
                            // If caller wants blank-not-counted, they can not add cell; here blank is empty <c>
                            if (cellSpec.EmitBlankCellTag)
                                row.AppendChild(cell);
                            continue;
                        }

                        if (cellSpec.InlineString)
                        {
                            cell.DataType = CellValues.InlineString;
                            cell.InlineString = new InlineString(
                                new Text(cellSpec.TextValue ?? string.Empty)
                            );
                        }
                        else if (cellSpec.UseSharedString)
                        {
                            var idx = GetSharedStringIndex(cellSpec.TextValue ?? string.Empty);
                            cell.DataType = CellValues.SharedString;
                            cell.CellValue = new CellValue(
                                idx.ToString(CultureInfo.InvariantCulture)
                            );
                        }
                        else if (cellSpec.IsDate)
                        {
                            // Date cell: store OADate double, style index 1 (date)
                            var oa = cellSpec.DateValue!.Value.ToOADate();
                            // Adjust for 1904? OADate is always 1900-based; Excel 1904 stores offset 1462
                            if (_use1904DateSystem)
                                oa -= 1462;
                            cell.CellValue = new CellValue(
                                oa.ToString(CultureInfo.InvariantCulture)
                            );
                            cell.StyleIndex = 1;
                        }
                        else if (cellSpec.IsFormula)
                        {
                            cell.CellFormula = new CellFormula(cellSpec.FormulaText);
                            // cached value
                            if (cellSpec.UseSharedString && cellSpec.TextValue != null)
                            {
                                var idx = GetSharedStringIndex(cellSpec.TextValue);
                                cell.DataType = CellValues.SharedString;
                                cell.CellValue = new CellValue(
                                    idx.ToString(CultureInfo.InvariantCulture)
                                );
                            }
                            else
                            {
                                cell.CellValue = new CellValue(cellSpec.CachedValue ?? "0");
                            }
                        }
                        else
                        {
                            // Plain text or numeric
                            if (cellSpec.TextValue != null)
                                cell.CellValue = new CellValue(cellSpec.TextValue);
                            if (cellSpec.DataType != null)
                                cell.DataType = cellSpec.DataType;
                        }

                        if (cellSpec.StyleIndex.HasValue)
                            cell.StyleIndex = cellSpec.StyleIndex.Value;

                        row.AppendChild(cell);
                    }
                    sheetData.AppendChild(row);
                    rowIndex++;
                }

                wsPart.Worksheet = worksheet;

                var sheet = new Sheet
                {
                    Id = workbookPart.GetIdOfPart(wsPart),
                    SheetId = sheetId++,
                    Name = spec.Name,
                };
                if (spec.State.HasValue)
                    sheet.State = spec.State.Value;
                workbookPart.Workbook.Sheets.AppendChild(sheet);
            }

            workbookPart.Workbook.Save();
        }

        // Patch hidden-only if needed
        if (needsPatchHiddenOnly && patchTargets != null)
        {
            // Re-open zip and set sheet state to hidden for the first sheet
            stream.Position = 0;
            using var zip = new System.IO.Compression.ZipArchive(
                stream,
                System.IO.Compression.ZipArchiveMode.Update,
                leaveOpen: true
            );
            var entry = zip.GetEntry("xl/workbook.xml");
            if (entry != null)
            {
                using var reader = new StreamReader(entry.Open());
                var xml = reader.ReadToEnd();
                // Replace first sheet without state to hidden
                xml = xml.Replace(
                    $"<sheet ",
                    $"<sheet state=\"hidden\" ",
                    StringComparison.Ordinal
                );
                // If savedFirstState was VeryHidden, adjust
                if (savedFirstState == SheetStateValues.VeryHidden)
                    xml = xml.Replace("state=\"hidden\"", "state=\"veryHidden\"");
                entry.Delete();
                var newEntry = zip.CreateEntry("xl/workbook.xml");
                using var writer = new StreamWriter(newEntry.Open());
                writer.Write(xml);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static string GetColumnLetter(int col)
    {
        var letters = string.Empty;
        while (col > 0)
        {
            col--;
            letters = (char)('A' + (col % 26)) + letters;
            col /= 26;
        }
        return letters;
    }

    public sealed class SheetBuilder
    {
        private readonly XlsxWorkbookBuilder _parent;
        private readonly SheetSpec _spec;

        internal SheetBuilder(XlsxWorkbookBuilder parent, SheetSpec spec)
        {
            _parent = parent;
            _spec = spec;
        }

        public SheetBuilder AddRow(params CellSpec[] cells)
        {
            _spec.Rows.Add(new RowSpec { Cells = cells.ToList() });
            return this;
        }

        public SheetBuilder AddRow(IEnumerable<CellSpec> cells)
        {
            _spec.Rows.Add(new RowSpec { Cells = cells.ToList() });
            return this;
        }

        public SheetBuilder AddRows(int count, Func<int, IEnumerable<CellSpec>> factory)
        {
            for (int i = 0; i < count; i++)
                AddRow(factory(i).ToArray());
            return this;
        }

        public SheetBuilder WithMergedRange(string range)
        {
            _spec.MergedRanges.Add(range);
            return this;
        }

        public XlsxWorkbookBuilder EndSheet() => _parent;
    }

    internal sealed class SheetSpec
    {
        public string Name { get; set; } = string.Empty;
        public SheetStateValues? State { get; set; }
        public List<RowSpec> Rows { get; } = new();
        public List<string> MergedRanges { get; } = new();
    }

    internal sealed class RowSpec
    {
        public List<CellSpec> Cells { get; set; } = new();
    }

    public sealed class CellSpec
    {
        public string? TextValue { get; set; }
        public bool UseSharedString { get; set; }
        public bool InlineString { get; set; }
        public bool IsDate { get; set; }
        public DateTime? DateValue { get; set; }
        public bool IsFormula { get; set; }
        public string? FormulaText { get; set; }
        public string? CachedValue { get; set; }
        public EnumValue<CellValues>? DataType { get; set; }
        public uint? StyleIndex { get; set; }
        public bool IsBlank { get; set; }
        public bool EmitBlankCellTag { get; set; }

        public static CellSpec Text(string text, bool shared = true) =>
            new() { TextValue = text, UseSharedString = shared };

        public static CellSpec Inline(string text) =>
            new() { TextValue = text, InlineString = true };

        public static CellSpec Number(string n) => new() { TextValue = n };

        public static CellSpec Formula(string formula, string cached) =>
            new()
            {
                IsFormula = true,
                FormulaText = formula,
                CachedValue = cached,
            };

        public static CellSpec Date(DateTime dt) => new() { IsDate = true, DateValue = dt };

        public static CellSpec Blank(bool emitTag = false) =>
            new() { IsBlank = true, EmitBlankCellTag = emitTag };
    }
}
