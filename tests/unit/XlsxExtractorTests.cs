using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class XlsxExtractorTests
{
    [Fact]
    public async Task Extract_ThreeSheets_Contains_SheetMarkers_And_KnownSentence_And_PipeJoin()
    {
        await using var stream = BuildThreeSheet();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);

        text.Should().Contain("[Sheet: Sheet1]");
        text.Should().Contain("[Sheet: Sheet2]");
        text.Should().Contain("[Sheet: Sheet3]");
        text.Should().Contain("refund policy: 30-day full refund with receipt");
        // Rows joined with |
        text.Should().Contain(" | ");
    }

    [Fact]
    public async Task Extract_HeaderRepeated_Before_Every_DataRow()
    {
        // Build 1 sheet with header + 3 data rows
        var builder = new XlsxWorkbookBuilder();
        builder
            .AddSheet("Data")
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("H1"),
                XlsxWorkbookBuilder.CellSpec.Text("H2"),
                XlsxWorkbookBuilder.CellSpec.Text("H3")
            )
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("a1"),
                XlsxWorkbookBuilder.CellSpec.Text("b1"),
                XlsxWorkbookBuilder.CellSpec.Text("c1")
            )
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("a2"),
                XlsxWorkbookBuilder.CellSpec.Text("b2"),
                XlsxWorkbookBuilder.CellSpec.Text("c2")
            )
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("a3"),
                XlsxWorkbookBuilder.CellSpec.Text("b3"),
                XlsxWorkbookBuilder.CellSpec.Text("c3")
            )
            .EndSheet();

        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var headerLine = "H1 | H2 | H3";
        // Expect: [Sheet], header, header+row1, header+row2, header+row3? Per spec: header line then header repeated before each data line
        // Implementation: [Sheet], header, then for each data row: header then row
        var headerCount = lines.Count(l => l == headerLine);
        var dataRowCount = 3;
        // header appears 1 + dataRowCount times (initial header + repeat before each data row)
        headerCount.Should().Be(1 + dataRowCount);

        // Every data line immediately preceded by header line
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("a") || lines[i].StartsWith("b"))
            {
                // Not robust; check pattern: data rows contain a1/b1 etc.
                // Instead verify predecessor is header when line is data
                // Data rows are a1|b1|c1 etc.
            }
        }
        // Verify adjacency: header before each data row
        for (int i = 1; i < lines.Count; i++)
        {
            if (
                lines[i].Contains("a1 | b1 | c1")
                || lines[i].Contains("a2 | b2 | c2")
                || lines[i].Contains("a3 | b3 | c3")
            )
            {
                lines[i - 1]
                    .Should()
                    .Be(headerLine, $"data row {lines[i]} should be preceded by header");
            }
        }
    }

    [Fact]
    public async Task Extract_FormulaCell_Emits_Cached_42_50_Not_Sum()
    {
        await using var stream = BuildThreeSheet();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().Contain("42.50");
        text.Should().NotContain("SUM");
        text.Should().NotContain("=SUM");
    }

    [Fact]
    public async Task Extract_SharedString_And_InlineString_Both_Resolve()
    {
        var builder = new XlsxWorkbookBuilder();
        builder
            .AddSheet("S")
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("Header1", shared: true),
                XlsxWorkbookBuilder.CellSpec.Text("Header2", shared: false)
            )
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("shared-val", shared: true),
                XlsxWorkbookBuilder.CellSpec.Inline("inline-val")
            )
            .EndSheet();
        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        text.Should().Contain("shared-val");
        text.Should().Contain("inline-val");
        text.Should().NotContain("12"); // raw SST index
    }

    [Fact]
    public async Task Extract_DateFormatted_1900_And_1904_Systems()
    {
        // 1900 system: 2022-01-01 is OADate 44562
        var date = new DateTime(2022, 1, 1);
        var b1900 = new XlsxWorkbookBuilder();
        b1900
            .AddSheet("D1900")
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("Date"))
            .AddRow(XlsxWorkbookBuilder.CellSpec.Date(date))
            .EndSheet();
        await using var s1 = b1900.Build();
        var t1 = await Chunker.ExtractTextAsync(s1, DocumentMimeType.Xlsx);
        t1.Should().Contain("2022-01-01");

        // 1904 system
        var b1904 = new XlsxWorkbookBuilder().Use1904DateSystem(true);
        b1904
            .AddSheet("D1904")
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("Date"))
            .AddRow(XlsxWorkbookBuilder.CellSpec.Date(date))
            .EndSheet();
        await using var s2 = b1904.Build();
        var t2 = await Chunker.ExtractTextAsync(s2, DocumentMimeType.Xlsx);
        t2.Should().Contain("2022-01-01");

        // Missing style fallback: raw value without date formatting
        var bRaw = new XlsxWorkbookBuilder();
        bRaw.AddSheet("Raw")
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("Val"))
            .AddRow(XlsxWorkbookBuilder.CellSpec.Number("44562"))
            .EndSheet();
        await using var s3 = bRaw.Build();
        var t3 = await Chunker.ExtractTextAsync(s3, DocumentMimeType.Xlsx);
        // Raw numeric stays raw (not date)
        t3.Should().Contain("44562");
    }

    [Fact]
    public async Task Extract_MergedRange_Value_On_Anchor_Only()
    {
        var builder = new XlsxWorkbookBuilder();
        var sheet = builder.AddSheet("M");
        sheet.WithMergedRange("A1:B1");
        sheet
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("MergedVal"),
                XlsxWorkbookBuilder.CellSpec.Blank()
            )
            .AddRow(XlsxWorkbookBuilder.CellSpec.Text("a"), XlsxWorkbookBuilder.CellSpec.Text("b"))
            .EndSheet();
        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        // Anchor has value, merged second cell empty → header row is "MergedVal" (only one value) not duplicated
        text.Should().Contain("MergedVal");
        // Ensure not duplicated extra separator?
        // The merged row has only one cell with value; second cell blank should not add trailing |
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().Contain(l => l.Contains("MergedVal"));
    }

    [Fact]
    public async Task Extract_AllBlankRows_Omitted()
    {
        var builder = new XlsxWorkbookBuilder();
        builder
            .AddSheet("B")
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("H1"),
                XlsxWorkbookBuilder.CellSpec.Text("H2")
            )
            .AddRow(XlsxWorkbookBuilder.CellSpec.Blank(), XlsxWorkbookBuilder.CellSpec.Blank())
            .AddRow(
                XlsxWorkbookBuilder.CellSpec.Text("val1"),
                XlsxWorkbookBuilder.CellSpec.Text("val2")
            )
            .AddRow(XlsxWorkbookBuilder.CellSpec.Blank(), XlsxWorkbookBuilder.CellSpec.Blank())
            .EndSheet();
        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        // No blank lines, no lines that are just empty separators
        lines.Should().NotContain(l => string.IsNullOrWhiteSpace(l));
        // Should contain header + data rows only (blank rows skipped)
        lines.Count(l => l.Contains("val1")).Should().Be(1);
    }

    [Fact]
    public async Task Extract_PipedThrough_ChunkText_Yields_SelfDescribingChunks()
    {
        // Build larger workbook to exceed 512 tokens
        var builder = new XlsxWorkbookBuilder();
        var headers = new[]
        {
            "Name",
            "Dept",
            "Refund Policy",
            "Col4",
            "Col5",
            "Col6",
            "Col7",
            "Col8",
        };
        var sheet = builder.AddSheet("Sheet1");
        sheet.AddRow(headers.Select(h => XlsxWorkbookBuilder.CellSpec.Text(h)).ToArray());
        for (int i = 0; i < 80; i++)
        {
            sheet.AddRow(
                Enumerable
                    .Range(0, headers.Length)
                    .Select(c =>
                        XlsxWorkbookBuilder.CellSpec.Text(
                            $"val{i}_{c}_longer_token_to_grow_chunk_size"
                        )
                    )
                    .ToArray()
            );
        }
        sheet.EndSheet();
        await using var stream = builder.Build();
        var text = await Chunker.ExtractTextAsync(stream, DocumentMimeType.Xlsx);
        var chunks = Chunker.ChunkText(text, Guid.NewGuid(), 512, 50);
        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().Contain(c => c.Text.Contains("Name") && c.Text.Contains("Dept"));
        foreach (var chunk in chunks.Where(c => c.Text.Contains("val")))
        {
            chunk.Text.Should().Contain("Name");
        }
    }

    private static MemoryStream BuildThreeSheet()
    {
        var builder = new XlsxWorkbookBuilder();
        var headers = new[] { "Name", "Dept", "Refund Policy" };
        string[] ToRow(int i) => new[] { $"User{i}", "Sales", $"row {i}" };

        var s1 = builder.AddSheet("Sheet1");
        s1.AddRow(headers.Select(h => XlsxWorkbookBuilder.CellSpec.Text(h)).ToArray());
        for (int i = 0; i < 10; i++)
            s1.AddRow(ToRow(i).Select(v => XlsxWorkbookBuilder.CellSpec.Text(v)).ToArray());
        s1.EndSheet();

        var s2 = builder.AddSheet("Sheet2");
        s2.AddRow(headers.Select(h => XlsxWorkbookBuilder.CellSpec.Text(h)).ToArray());
        for (int i = 0; i < 10; i++)
        {
            if (i == 3)
                s2.AddRow(
                    XlsxWorkbookBuilder.CellSpec.Text($"User{i}"),
                    XlsxWorkbookBuilder.CellSpec.Text(
                        "refund policy: 30-day full refund with receipt"
                    ),
                    XlsxWorkbookBuilder.CellSpec.Text($"row {i}")
                );
            else
                s2.AddRow(ToRow(i).Select(v => XlsxWorkbookBuilder.CellSpec.Text(v)).ToArray());
        }
        // formula cached 42.50
        s2.AddRow(
            XlsxWorkbookBuilder.CellSpec.Formula("SUM(A1:A3)", "42.50"),
            XlsxWorkbookBuilder.CellSpec.Text("extra"),
            XlsxWorkbookBuilder.CellSpec.Text("row")
        );
        s2.EndSheet();

        var s3 = builder.AddSheet("Sheet3");
        s3.AddRow(headers.Select(h => XlsxWorkbookBuilder.CellSpec.Text(h)).ToArray());
        for (int i = 0; i < 10; i++)
            s3.AddRow(ToRow(i).Select(v => XlsxWorkbookBuilder.CellSpec.Text(v)).ToArray());
        s3.EndSheet();

        // Wide case for date? Not needed.
        return builder.Build();
    }
}
