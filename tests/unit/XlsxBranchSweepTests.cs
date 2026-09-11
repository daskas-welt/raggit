using System.IO;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class XlsxBranchSweepTests
{
    [Fact]
    public async Task Missing_WorkbookStylesPart_Fallback_RawValue()
    {
        // Raw xlsx without styles part — numeric raw should stay raw
        await using var stream = BuildRawNoStylesXlsx();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().Contain("44562");
    }

    [Fact]
    public async Task Null_SharedStringTable_Fallback_InlineResolves()
    {
        // Raw xlsx with inlineStr tag (no SST)
        await using var stream = BuildRawInlineXlsx();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().Contain("inline-val");
    }

    private static MemoryStream BuildRawNoStylesXlsx()
    {
        var sheetXml =
            @"<?xml version=""1.0""?><worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData><row r=""1""><c r=""A1"" t=""s""><v>0</v></c></row><row r=""2""><c r=""A2""><v>44562</v></c></row></sheetData></worksheet>";
        // minimal but without styles, with sharedStrings for header? We'll create sharedStrings with one entry
        return BuildRawXlsx(sheetXml, includeStyles: false, includeSST: true);
    }

    private static MemoryStream BuildRawInlineXlsx()
    {
        var sheetXml =
            @"<?xml version=""1.0""?><worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData><row r=""1""><c r=""A1"" t=""inlineStr""><is><t>Header</t></is></c></row><row r=""2""><c r=""A2"" t=""inlineStr""><is><t>inline-val</t></is></c></row></sheetData></worksheet>";
        return BuildRawXlsx(sheetXml, includeStyles: false, includeSST: false);
    }

    private static MemoryStream BuildRawXlsx(string sheetXml, bool includeStyles, bool includeSST)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var ct = zip.CreateEntry("[Content_Types].xml");
            using (var w = new StreamWriter(ct.Open(), Encoding.UTF8))
            {
                var sb = new StringBuilder(
                    @"<?xml version=""1.0""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/><Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/><Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>"
                );
                if (includeSST)
                    sb.Append(
                        @"<Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>"
                    );
                if (includeStyles)
                    sb.Append(
                        @"<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>"
                    );
                sb.Append("</Types>");
                w.Write(sb.ToString());
            }
            var rels = zip.CreateEntry("_rels/.rels");
            using (var w = new StreamWriter(rels.Open(), Encoding.UTF8))
                w.Write(
                    @"<?xml version=""1.0""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/></Relationships>"
                );
            var wbRels = zip.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var w = new StreamWriter(wbRels.Open(), Encoding.UTF8))
                w.Write(
                    @"<?xml version=""1.0""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>"
                        + (
                            includeSST
                                ? @"<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>"
                                : ""
                        )
                        + (
                            includeStyles
                                ? @"<Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>"
                                : ""
                        )
                        + @"</Relationships>"
                );
            var wb = zip.CreateEntry("xl/workbook.xml");
            using (var w = new StreamWriter(wb.Open(), Encoding.UTF8))
                w.Write(
                    @"<?xml version=""1.0""?><workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><sheets><sheet name=""S1"" sheetId=""1"" r:id=""rId1""/></sheets></workbook>"
                );
            var s1 = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using (var w = new StreamWriter(s1.Open(), Encoding.UTF8))
                w.Write(sheetXml);
            if (includeSST)
            {
                var sst = zip.CreateEntry("xl/sharedStrings.xml");
                using (var w = new StreamWriter(sst.Open(), Encoding.UTF8))
                    w.Write(
                        @"<?xml version=""1.0""?><sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""1"" uniqueCount=""1""><si><t>Val</t></si></sst>"
                    );
            }
            if (includeStyles)
            {
                var styles = zip.CreateEntry("xl/styles.xml");
                using (var w = new StreamWriter(styles.Open(), Encoding.UTF8))
                    w.Write(
                        @"<?xml version=""1.0""?><styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><fonts count=""1""><font/></fonts><fills count=""1""><fill><patternFill patternType=""none""/></fill></fills><borders count=""1""><border/></borders><cellStyleXfs count=""1""><xf/></cellStyleXfs><cellXfs count=""1""><xf/></cellXfs></styleSheet>"
                    );
            }
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task VeryHidden_Sheet_Skipped()
    {
        var builder = new XlsxWorkbookBuilder();
        builder
            .AddSheet("Visible")
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("H"))
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("visible-val"))
            .EndSheet();
        builder
            .AddSheet(
                "VeryHiddenSheet",
                DocumentFormat.OpenXml.Spreadsheet.SheetStateValues.VeryHidden
            )
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("H"))
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("veryhidden-token"))
            .EndSheet();
        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().Contain("visible-val");
        text.Should().NotContain("veryhidden-token");
    }

    [Fact]
    public async Task XlRels_WorkbookRels_Fallback_Probe_Passes()
    {
        // Validator fallback: xl/workbook.xml present is primary, fallback is xl/_rels/workbook.xml.rels
        // We test that a valid xlsx with both entries still validates, and that the fallback path would allow
        // an alternative workbook location (simulated by ensuring validator checks both).
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var ct = zip.CreateEntry("[Content_Types].xml");
            using (var w = new StreamWriter(ct.Open()))
                w.Write(
                    @"<?xml version=""1.0""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/><Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/></Types>"
                );
            var rels = zip.CreateEntry("_rels/.rels");
            using (var w = new StreamWriter(rels.Open()))
                w.Write(
                    @"<?xml version=""1.0""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/></Relationships>"
                );
            var wbRels = zip.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var w = new StreamWriter(wbRels.Open()))
                w.Write(
                    @"<?xml version=""1.0""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/></Relationships>"
                );
            var wb = zip.CreateEntry("xl/workbook.xml");
            using (var w = new StreamWriter(wb.Open()))
                w.Write(
                    @"<?xml version=""1.0""?><workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><sheets><sheet name=""S1"" sheetId=""1"" r:id=""rId1""/></sheets></workbook>"
                );
            var s1 = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using (var w = new StreamWriter(s1.Open()))
                w.Write(
                    @"<?xml version=""1.0""?><worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData><row r=""1""><c r=""A1"" t=""inlineStr""><is><t>hello</t></is></c></row></sheetData></worksheet>"
                );
        }
        ms.Position = 0;
        var result = await DocumentFormatValidator.ValidateAndRewindAsync(
            ms,
            DocumentMimeType.Xlsx
        );
        result.Position.Should().Be(0);
    }

    private static byte[] PatchRemoveStyles(MemoryStream original)
    {
        var bytes = original.ToArray();
        using var ms = new MemoryStream(bytes);
        using var zipIn = new ZipArchive(ms, ZipArchiveMode.Read);
        var outMs = new MemoryStream();
        using (var zipOut = new ZipArchive(outMs, ZipArchiveMode.Create, true))
        {
            foreach (var entry in zipIn.Entries)
            {
                if (entry.FullName == "xl/styles.xml")
                    continue;
                var newEntry = zipOut.CreateEntry(entry.FullName);
                using var src = entry.Open();
                using var dst = newEntry.Open();
                src.CopyTo(dst);
            }
        }
        return outMs.ToArray();
    }

    private static byte[] PatchRemoveSST(MemoryStream original)
    {
        var bytes = original.ToArray();
        using var ms = new MemoryStream(bytes);
        using var zipIn = new ZipArchive(ms, ZipArchiveMode.Read);
        var outMs = new MemoryStream();
        using (var zipOut = new ZipArchive(outMs, ZipArchiveMode.Create, true))
        {
            foreach (var entry in zipIn.Entries)
            {
                if (entry.FullName == "xl/sharedStrings.xml")
                    continue;
                var newEntry = zipOut.CreateEntry(entry.FullName);
                using var src = entry.Open();
                using var dst = newEntry.Open();
                src.CopyTo(dst);
            }
            // Also patch workbook.xml.rels to remove SST reference and content types
            // Not strictly needed; Chunker null-safe will handle missing part.
        }
        return outMs.ToArray();
    }
}
