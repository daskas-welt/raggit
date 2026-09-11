using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class XlsxCellCapTests
{
    [Fact]
    public async Task Exactly_100k_Cells_Succeeds()
    {
        await using var stream = GenerateXlsx(100_000, visibleOnly: true);
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task At_100001_Cells_Throws_CapExceeded_With_Message_Naming_Cap_And_Actual()
    {
        await using var stream = GenerateXlsx(100_001, visibleOnly: true);
        var act = async () => await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        var ex = await Assert.ThrowsAsync<SpreadsheetCellCapExceededException>(act);
        ex.Message.Should().Contain("100,000");
        ex.ActualCount.Should().BeGreaterThan(100_000);
    }

    [Fact]
    public async Task Hidden_Sheet_Cells_Excluded_From_Count_80kVisible_Plus_50kHidden_Passes()
    {
        // 80k visible + 50k hidden => should pass because hidden excluded
        await using var stream = GenerateXlsxWithHidden(80_000, 50_000);
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().NotBeNullOrEmpty();
        text.Should().NotContain("hidden-token");
    }

    [Fact]
    public async Task WideSparseRow_1x20k_Counts_Toward_Cap()
    {
        await using var stream = GenerateXlsx(20_000, visibleOnly: true);
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WideSparseRow_OverCap_Throws()
    {
        await using var stream = GenerateXlsx(100_001, visibleOnly: true);
        var act = async () => await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        await Assert.ThrowsAsync<SpreadsheetCellCapExceededException>(act);
    }

    // Helpers — generate minimal valid xlsx with n <c> cells across visible sheets
    private static MemoryStream GenerateXlsx(int totalCells, bool visibleOnly)
    {
        // Distribute cells as rows of 20 cols
        int cols = 20;
        int rows = (totalCells + cols - 1) / cols;
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        int remaining = totalCells;
        for (int r = 1; r <= rows && remaining > 0; r++)
        {
            sb.Append($"<row r=\"{r}\">");
            for (int c = 1; c <= cols && remaining > 0; c++)
            {
                var colLetter = GetColLetter(c);
                sb.Append($"<c r=\"{colLetter}{r}\"><v>{r * cols + c}</v></c>");
                remaining--;
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        var sheetXml = sb.ToString();
        return BuildXlsxWithSheetXml(sheetXml, hiddenSheetXml: null);
    }

    private static MemoryStream GenerateXlsxWithHidden(int visibleCells, int hiddenCells)
    {
        var visibleXml = BuildSheetXml(visibleCells, "visible");
        var hiddenXml = BuildSheetXml(hiddenCells, "hidden-token");
        return BuildXlsxWithSheetXml(visibleXml, hiddenXml);
    }

    private static string BuildSheetXml(int cellCount, string prefix)
    {
        int cols = 20;
        int rows = (cellCount + cols - 1) / cols;
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        int remaining = cellCount;
        for (int r = 1; r <= rows && remaining > 0; r++)
        {
            sb.Append($"<row r=\"{r}\">");
            for (int c = 1; c <= cols && remaining > 0; c++)
            {
                var colLetter = GetColLetter(c);
                sb.Append($"<c r=\"{colLetter}{r}\"><v>{prefix}_{r}_{c}</v></c>");
                remaining--;
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static MemoryStream BuildXlsxWithSheetXml(string sheet1Xml, string? hiddenSheetXml)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // [Content_Types].xml
            var ct = zip.CreateEntry("[Content_Types].xml");
            using (var w = new StreamWriter(ct.Open(), Encoding.UTF8))
                w.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?><Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types""><Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/><Default Extension=""xml"" ContentType=""application/xml""/><Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/><Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" + (hiddenSheetXml != null ? @"<Override PartName=""/xl/worksheets/sheet2.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" : "") + @"</Types>");

            // _rels/.rels
            var rels = zip.CreateEntry("_rels/.rels");
            using (var w = new StreamWriter(rels.Open(), Encoding.UTF8))
                w.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/></Relationships>");

            // xl/_rels/workbook.xml.rels
            var wbRels = zip.CreateEntry("xl/_rels/workbook.xml.rels");
            using (var w = new StreamWriter(wbRels.Open(), Encoding.UTF8))
            {
                var relsXml = @"<?xml version=""1.0"" encoding=""UTF-8""?><Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>";
                if (hiddenSheetXml != null) relsXml += @"<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet2.xml""/>";
                relsXml += @"</Relationships>";
                w.Write(relsXml);
            }

            // xl/workbook.xml
            var wb = zip.CreateEntry("xl/workbook.xml");
            using (var w = new StreamWriter(wb.Open(), Encoding.UTF8))
            {
                var wbXml = @"<?xml version=""1.0"" encoding=""UTF-8""?><workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><sheets><sheet name=""Sheet1"" sheetId=""1"" r:id=""rId1""/>";
                if (hiddenSheetXml != null) wbXml += @"<sheet name=""Hidden"" sheetId=""2"" r:id=""rId2"" state=""hidden""/>";
                wbXml += @"</sheets></workbook>";
                w.Write(wbXml);
            }

            // xl/worksheets/sheet1.xml
            var s1 = zip.CreateEntry("xl/worksheets/sheet1.xml");
            using (var w = new StreamWriter(s1.Open(), Encoding.UTF8)) w.Write(sheet1Xml);

            if (hiddenSheetXml != null)
            {
                var s2 = zip.CreateEntry("xl/worksheets/sheet2.xml");
                using (var w = new StreamWriter(s2.Open(), Encoding.UTF8)) w.Write(hiddenSheetXml);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static string GetColLetter(int c)
    {
        var s = "";
        while (c > 0) { c--; s = (char)('A' + (c % 26)) + s; c /= 26; }
        return s;
    }
}
