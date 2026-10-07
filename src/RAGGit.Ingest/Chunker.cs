using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RAGGit.Core.Models;
using UglyToad.PdfPig;

namespace RAGGit.Ingest;

/// <summary>
/// Extracts plain text from supported document formats and splits it into
/// overlapping token windows per data-model.md.
/// </summary>
public static class Chunker
{
    /// <summary>
    /// Extracts raw text from a stream based on the document MIME type.
    /// </summary>
    public static async Task<string> ExtractTextAsync(
        Stream stream,
        DocumentMimeType mime,
        int maxSpreadsheetCells = DocumentValidation.MaxSpreadsheetCells
    )
    {
        if (maxSpreadsheetCells <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpreadsheetCells));
        }

        return mime switch
        {
            DocumentMimeType.Pdf => ExtractPdfText(stream),
            DocumentMimeType.Docx => ExtractDocxText(stream),
            DocumentMimeType.Xlsx => await ExtractXlsxTextAsync(stream, maxSpreadsheetCells),
            DocumentMimeType.Txt or DocumentMimeType.Md => await ExtractPlainTextAsync(stream),
            _ => throw new NotSupportedException($"Unsupported MIME type: {mime}"),
        };
    }

    private static Task<string> ExtractXlsxTextAsync(Stream stream, int maxSpreadsheetCells)
    {
        try
        {
            if (stream.CanSeek && stream.Position != 0)
                stream.Position = 0;

            // Copy to seekable MemoryStream if needed (SpreadsheetDocument requires seekable)
            Stream seekable = stream;
            if (!stream.CanSeek)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                seekable = ms;
            }
            else
            {
                // Ensure we work on a copy to avoid disposing original? SpreadsheetDocument with leaveOpen false will close.
                // Use MemoryStream copy to keep original stream open for caller? But ExtractTextAsync is pure and stream is ingestion copy.
                // We'll open with leaveOpen handling via closing seekable as appropriate.
            }

            // SpreadsheetDocument.Open requires a seekable stream; we keep original position at 0 after.
            using var document = SpreadsheetDocument.Open(seekable, false);
            var workbookPart = document.WorkbookPart;
            if (workbookPart == null)
                throw new CorruptDocumentException("content does not match type");

            var workbook = workbookPart.Workbook;
            var sheets = workbook.Sheets?.Elements<Sheet>().ToList() ?? new List<Sheet>();
            if (sheets.Count == 0)
                return Task.FromResult(string.Empty);

            // Shared strings
            SharedStringTable? sst = null;
            try
            {
                sst = workbookPart.SharedStringTablePart?.SharedStringTable;
            }
            catch
            {
                sst = null;
            }

            // Date1904
            var is1904 = workbook.WorkbookProperties?.Date1904?.Value ?? false;
            // Also check Workbook.WorkbookProperties via alternate path
            if (!is1904 && workbook.GetFirstChild<WorkbookProperties>()?.Date1904?.Value == true)
                is1904 = true;

            // Styles / Number formats for date detection
            var stylesPart = workbookPart.WorkbookStylesPart;
            Dictionary<uint, string> customFormats = new();
            List<CellFormat> cellFormats = new();
            if (stylesPart?.Stylesheet != null)
            {
                var nfs = stylesPart.Stylesheet.NumberingFormats;
                if (nfs != null)
                {
                    foreach (var nf in nfs.Elements<NumberingFormat>())
                    {
                        if (nf.NumberFormatId != null && nf.FormatCode != null)
                            customFormats[(uint)nf.NumberFormatId.Value] =
                                nf.FormatCode.Value ?? string.Empty;
                    }
                }
                var cfs = stylesPart.Stylesheet.CellFormats;
                if (cfs != null)
                {
                    foreach (var cf in cfs.Elements<CellFormat>())
                        cellFormats.Add(cf);
                }
            }

            bool IsDateFormat(uint numFmtId)
            {
                // Built-in date ranges
                if (
                    (numFmtId >= 14 && numFmtId <= 22)
                    || (numFmtId >= 27 && numFmtId <= 36)
                    || (numFmtId >= 45 && numFmtId <= 47)
                    || (numFmtId >= 50 && numFmtId <= 58)
                )
                    return true;
                if (customFormats.TryGetValue(numFmtId, out var code))
                {
                    var lower = code.ToLowerInvariant();
                    // heuristic: contains y/m/d/h with date-like pattern
                    if (
                        lower.Contains("yy")
                        || lower.Contains("yyyy")
                        || lower.Contains("mm")
                        || lower.Contains("dd")
                    )
                        return true;
                    if (lower.Contains("y") && lower.Contains("m"))
                        return true;
                }
                return false;
            }

            var lines = new List<string>();
            long visibleCellCount = 0;

            foreach (var sheet in sheets)
            {
                // Hidden check
                var state = sheet.State?.Value;
                if (state == SheetStateValues.Hidden || state == SheetStateValues.VeryHidden)
                    continue;

                var sheetName = sheet.Name?.Value ?? "Sheet";
                var wsPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                // SAX reader over worksheet
                var rowsData = new List<List<string>>();

                using (var reader = OpenXmlReader.Create(wsPart))
                {
                    while (reader.Read())
                    {
                        if (reader.ElementType == typeof(Row))
                        {
                            var row = (Row)reader.LoadCurrentElement();
                            var cellTextsForRow = new List<string>();
                            foreach (var cell in row.Elements<Cell>())
                            {
                                visibleCellCount++;
                                if (visibleCellCount > maxSpreadsheetCells)
                                {
                                    throw new SpreadsheetCellCapExceededException(
                                        maxSpreadsheetCells,
                                        visibleCellCount
                                    );
                                }

                                var text = GetCellText(
                                    cell,
                                    sst,
                                    is1904,
                                    cellFormats,
                                    IsDateFormat
                                );
                                // Keep cellTexts ordered; include empty strings for blank cells? For compact join, skip empty
                                cellTextsForRow.Add(text ?? string.Empty);
                            }
                            // If row has no cells with <c>, it may be empty; but Elements<Cell>() empty → skip? Check row InnerText
                            // Keep row only if at least one non-whitespace cell
                            if (cellTextsForRow.Any(t => !string.IsNullOrWhiteSpace(t)))
                            {
                                rowsData.Add(cellTextsForRow);
                            }
                            else
                            {
                                // row with all blanks/whitespace → skip (do not add)
                            }
                        }
                    }
                }

                if (rowsData.Count == 0)
                    continue; // empty visible sheet

                // First non-empty row is header
                var headerTexts = rowsData[0];
                var headerLine = string.Join(
                    " | ",
                    headerTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim())
                );
                // If header line is empty after trim, treat next row as header? But per spec first non-empty row is header; if it's whitespace-only we already skipped.

                lines.Add($"[Sheet: {sheetName}]");
                lines.Add(headerLine);

                // Data rows: header repeated before each data row
                for (int i = 1; i < rowsData.Count; i++)
                {
                    var dataTexts = rowsData[i];
                    var dataLine = string.Join(
                        " | ",
                        dataTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim())
                    );
                    if (string.IsNullOrWhiteSpace(dataLine))
                        continue;
                    lines.Add(headerLine);
                    lines.Add(dataLine);
                }
            }

            // Cap already checked per-cell above; if over cap we threw.
            // If zero non-empty rows across visible sheets → caller (IngestService) will treat as no extractable content
            var result = string.Join(Environment.NewLine, lines);
            return Task.FromResult(result);
        }
        catch (SpreadsheetCellCapExceededException)
        {
            throw;
        }
        catch (CorruptDocumentException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is System.Xml.XmlException)
        {
            throw new CorruptDocumentException("content does not match type", ex);
        }
        catch (OpenXmlPackageException ex)
        {
            throw new CorruptDocumentException("content does not match type", ex);
        }
        catch (Exception ex)
        {
            // For any other failure during xlsx extraction, map to corrupt if it looks like zip/malformed
            if (
                ex.Message.Contains(
                    "content does not match type",
                    StringComparison.OrdinalIgnoreCase
                )
                || ex is System.IO.IOException
            )
                throw new CorruptDocumentException("content does not match type", ex);
            throw;
        }
    }

    private static string? GetCellText(
        Cell cell,
        SharedStringTable? sst,
        bool is1904,
        List<CellFormat> cellFormats,
        Func<uint, bool> isDateFormat
    )
    {
        // Formula: ignore formula text, use cached <v>
        var rawValue = cell.CellValue?.Text ?? cell.InnerText; // InnerText includes v + formula text? Use CellValue
        // For formula, CellValue is cached
        // Need to resolve DataType
        var dataType = cell.DataType?.Value;

        // InlineString
        if (dataType == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? rawValue;
        }

        // SharedString
        if (dataType == CellValues.SharedString)
        {
            if (
                int.TryParse(
                    rawValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var idx
                )
                && sst != null
            )
            {
                try
                {
                    var ssi = sst.ElementAt(idx);
                    // Concatenate all <t> for rich strings
                    return string.Concat(ssi.Descendants<Text>().Select(t => t.Text));
                }
                catch
                {
                    return rawValue;
                }
            }
            return rawValue;
        }

        // Date handling via style
        if (cell.StyleIndex != null)
        {
            // Try to detect date
            try
            {
                var styleIdx = (int)cell.StyleIndex.Value;
                if (styleIdx < cellFormats.Count)
                {
                    var fmt = cellFormats[styleIdx];
                    var numFmtId = fmt.NumberFormatId?.Value ?? 0;
                    if (isDateFormat((uint)numFmtId))
                    {
                        if (
                            double.TryParse(
                                rawValue,
                                NumberStyles.Any,
                                CultureInfo.InvariantCulture,
                                out var oa
                            )
                        )
                        {
                            // Excel 1904 offset: 1462 days difference, also leap bug for 1900
                            double adjusted = oa;
                            if (is1904)
                                adjusted += 1462;
                            // DateTime.FromOADate handles 1900 leap bug internally? Need to keep as-is.
                            try
                            {
                                var dt = DateTime.FromOADate(adjusted);
                                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }
        else if (false)
        {
            // No SST but still check date via customFormats built above (need fmt map)
            // Fallback: if style exists but we couldn't load formats, treat as date if raw is double-like and style suggests date
            // For unit test with no StylesPart, fallback to raw
        }

        // Handle booleans etc. as raw
        // If raw is null/empty, cell may be empty
        if (string.IsNullOrEmpty(rawValue))
        {
            // Might be inline string without DataType? Check InlineString element
            if (cell.InlineString != null)
                return cell.InlineString.InnerText;
            return string.Empty;
        }

        // SharedString fallback when DataType missing but SST present and value is integer index? Not needed.

        // For formula cells where dataType is SharedString, we already handled.
        // Otherwise return raw cached value
        return rawValue;
    }

    /// <summary>
    /// Splits text into chunks of <paramref name="chunkSize"/> tokens with
    /// <paramref name="overlap"/> tokens overlapping between consecutive chunks.
    /// Token counting is a whitespace-split approximation for v1.
    /// </summary>
    public static IReadOnlyList<Chunk> ChunkText(
        string text,
        Guid documentId,
        int chunkSize = 512,
        int overlap = 50
    )
    {
        ArgumentNullException.ThrowIfNull(text);

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSize),
                "Chunk size must be positive."
            );
        }

        if (overlap < 0 || overlap >= chunkSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlap),
                "Overlap must be non-negative and less than chunk size."
            );
        }

        var tokens = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return new List<Chunk>();
        }

        var step = chunkSize - overlap;
        var chunks = new List<Chunk>();
        var ordinal = 0;

        for (var start = 0; start < tokens.Length; start += step)
        {
            var length = Math.Min(chunkSize, tokens.Length - start);
            var chunkTokens = tokens[start..(start + length)];
            var chunkText = string.Join(' ', chunkTokens);

            chunks.Add(
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    Ordinal = ordinal++,
                    Text = chunkText,
                    TokenCount = length,
                }
            );
        }

        return chunks;
    }

    /// <summary>
    /// Groups consecutive child chunks into parent chunks (030, research R1).
    /// Each group of <paramref name="parentGroupSize"/> children yields one
    /// parent with a fresh id, <see cref="ChunkLevel.Parent"/>,
    /// <c>ParentId = null</c>, and <c>Ordinal</c> = group index; every
    /// grouped child is stamped with its parent's id (and
    /// <see cref="ChunkLevel.Child"/>). Parent text is the ordered
    /// concatenation of its children's texts with the known inter-child
    /// overlap trimmed — the first <c>min(chunkOverlap, childTokens-1)</c>
    /// tokens of every child after the first are dropped, so no sentence is
    /// duplicated. A trailing short group forms its own parent; a group size
    /// of 1 degrades to parent == child; empty input yields no parents.
    /// </summary>
    public static IReadOnlyList<Chunk> GroupIntoParents(
        IReadOnlyList<Chunk> children,
        int parentGroupSize = 4,
        int chunkOverlap = 50
    )
    {
        ArgumentNullException.ThrowIfNull(children);

        if (parentGroupSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parentGroupSize),
                "Parent group size must be at least 1."
            );
        }

        if (chunkOverlap < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkOverlap),
                "Chunk overlap must be non-negative."
            );
        }

        var parents = new List<Chunk>();
        for (var group = 0; group * parentGroupSize < children.Count; group++)
        {
            var groupChildren = children.Skip(group * parentGroupSize).Take(parentGroupSize).ToList();
            var parentId = Guid.NewGuid();
            var documentId = groupChildren[0].DocumentId;
            var text = BuildParentText(groupChildren, chunkOverlap);

            parents.Add(
                new Chunk
                {
                    Id = parentId,
                    DocumentId = documentId,
                    Ordinal = group,
                    Text = text,
                    TokenCount = CountTokens(text),
                    Level = ChunkLevel.Parent,
                    ParentId = null,
                }
            );

            foreach (var child in groupChildren)
            {
                child.Level = ChunkLevel.Child;
                child.ParentId = parentId;
            }
        }

        return parents;
    }

    private static string BuildParentText(IReadOnlyList<Chunk> groupChildren, int chunkOverlap)
    {
        var parts = new List<string>(groupChildren.Count);
        for (var i = 0; i < groupChildren.Count; i++)
        {
            var tokens = SplitTokens(groupChildren[i].Text);
            if (i > 0 && tokens.Length > 0)
            {
                var drop = Math.Min(chunkOverlap, tokens.Length - 1);
                tokens = tokens[drop..];
            }

            parts.Add(string.Join(' ', tokens));
        }

        return string.Join(' ', parts.Where(p => p.Length > 0));
    }

    private static string[] SplitTokens(string text) =>
        text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

    private static int CountTokens(string text) => SplitTokens(text).Length;

    /// <summary>
    /// Computes a SHA-256 hash for the stream contents and resets the stream
    /// position to zero when it is seekable.
    /// </summary>
    public static async Task<string> ComputeHashAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hash);
    }

    private static string ExtractPdfText(Stream stream)
    {
        try
        {
            var builder = new StringBuilder();
            using var document = PdfDocument.Open(stream);
            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
            }

            var text = builder.ToString();
            // Truncated PDFs may parse but yield no text — treat as corrupted if we produced no tokens and stream was non-empty
            if (string.IsNullOrWhiteSpace(text))
            {
                // Check if original stream had substantial bytes beyond header: if so, consider it corrupted
                if (stream.CanSeek && stream.Length > 100)
                {
                    // No text extracted from a non-trivial PDF → likely corrupted
                    throw new CorruptDocumentException("corrupted pdf");
                }
            }

            return text;
        }
        catch (CorruptDocumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CorruptDocumentException("corrupted pdf", ex);
        }
    }

    private static string ExtractDocxText(Stream stream)
    {
        try
        {
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document.Body;
            if (body is null)
            {
                return string.Empty;
            }

            var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join(Environment.NewLine, paragraphs);
        }
        catch (CorruptDocumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CorruptDocumentException("corrupted docx", ex);
        }
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );
        return await reader.ReadToEndAsync();
    }
}
