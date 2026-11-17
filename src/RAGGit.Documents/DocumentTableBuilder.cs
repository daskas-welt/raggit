using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>
/// Deterministically shapes the persisted answer into spreadsheet sections.
/// Repeated <c>label: value</c> blocks become a record table (union of labels,
/// one row per record, missing values empty); everything else becomes titled
/// section rows. No value is inferred and no model call is made (033 FR-003/008-011).
/// </summary>
public static class DocumentTableBuilder
{
    private static readonly Regex SourcePattern = new(@"\[(\d+)\]", RegexOptions.Compiled);
    private static readonly Regex PercentPattern = new(
        @"^\s*-?[\d.,]+\s*%\s*$",
        RegexOptions.Compiled
    );
    private static readonly Regex NumberPattern = new(
        @"^\s*[-+]?[\d.,]+\s*$",
        RegexOptions.Compiled
    );
    private static readonly CultureInfo Greek = CultureInfo.GetCultureInfo("el-GR");

    public static IReadOnlyList<DocumentSection> Build(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return Build(DocumentDraft.FromQueryDetail(detail));
    }

    public static IReadOnlyList<DocumentSection> Build(DocumentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var map = DocumentCitationMapper.Map(draft);
        var paragraphs = SplitParagraphs(map.Body);
        var sourceCount = map.Sources.Count;
        var clampSourceIndexes = !draft.PreserveUnmappedCitationMarkers;

        var records = new List<List<(string Label, string Value)>>();
        var prose = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            var pairs = ParsePairs(paragraph);
            if (pairs.Count >= 2)
            {
                records.Add(pairs);
            }
            else
            {
                prose.Add(paragraph);
            }
        }

        var sections = new List<DocumentSection>();
        if (records.Count > 0)
        {
            sections.Add(RecordSection(records, sourceCount, clampSourceIndexes));
        }

        if (prose.Count > 0)
        {
            sections.AddRange(SectionRows(prose, sourceCount, clampSourceIndexes));
        }

        return sections;
    }

    private static DocumentSection RecordSection(
        List<List<(string Label, string Value)>> records,
        int sourceCount,
        bool clampSourceIndexes
    )
    {
        var headers = records
            .SelectMany(record => record.Select(pair => pair.Label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = records
            .Select(record => new DocumentRow
            {
                Cells = headers
                    .Select(header =>
                    {
                        var value = record
                            .FirstOrDefault(pair =>
                                string.Equals(
                                    pair.Label,
                                    header,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .Value;
                        return ToCell(
                            header,
                            value ?? string.Empty,
                            sourceCount,
                            clampSourceIndexes
                        );
                    })
                    .ToList(),
            })
            .ToList();

        return new DocumentSection { Rows = rows, IsRecordShape = true };
    }

    private static IReadOnlyList<DocumentSection> SectionRows(
        IEnumerable<string> paragraphs,
        int sourceCount,
        bool clampSourceIndexes
    )
    {
        var sections = new List<DocumentSection>();
        foreach (var paragraph in paragraphs)
        {
            var lines = paragraph
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            // A short first line without terminal punctuation is a natural
            // section heading; otherwise the renderer supplies a localized
            // "Details" heading. One content column keeps prose readable.
            var hasHeading = lines.Count > 1 && IsHeading(lines[0]);
            var title = hasHeading ? lines[0] : string.Empty;
            var contentLines = hasHeading ? lines.Skip(1) : lines;

            var rows = contentLines
                .Select(line => new DocumentRow
                {
                    Cells = new[] { ToCell("Content", line, sourceCount, clampSourceIndexes) },
                })
                .ToList();

            sections.Add(
                new DocumentSection
                {
                    Title = title,
                    Rows = rows,
                    IsRecordShape = false,
                }
            );
        }

        return sections;
    }

    private static bool IsHeading(string line) =>
        line.Length <= 80
        && !line.Contains(':')
        && !line.EndsWith('.')
        && !line.EndsWith('!')
        && !line.EndsWith('?');

    private static List<(string Label, string Value)> ParsePairs(string paragraph)
    {
        var pairs = new List<(string Label, string Value)>();
        foreach (var rawLine in paragraph.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOfAny([':', '—', '–']);
            if (separator <= 0)
            {
                return new List<(string Label, string Value)>();
            }

            var label = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (label.Length == 0)
            {
                return new List<(string Label, string Value)>();
            }

            pairs.Add((label, NormalizeValue(value)));
        }

        return pairs;
    }

    private static string NormalizeValue(string value) =>
        string.Equals(value, "(empty)", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "—", StringComparison.Ordinal)
        || string.Equals(value, "–", StringComparison.Ordinal)
            ? string.Empty
            : value;

    private static DocumentCell ToCell(
        string header,
        string value,
        int sourceCount,
        bool clampSourceIndexes
    )
    {
        var sourceIndexes = SourcePattern
            .Matches(value)
            .Select(match => int.TryParse(match.Groups[1].Value, out var index) ? index : 0)
            .Where(index =>
                index > 0 && (!clampSourceIndexes || (sourceCount > 0 && index <= sourceCount))
            )
            .Distinct()
            .ToList();

        if (TryParseDate(value, out var date))
        {
            return new DocumentCell
            {
                Header = header,
                Text = value,
                Kind = DocumentCellKind.Date,
                DateValue = date,
                NumberFormat = "dd/MM/yyyy",
                SourceIndexes = sourceIndexes,
            };
        }

        if (TryParseNumber(value, out var number, out var format))
        {
            return new DocumentCell
            {
                Header = header,
                Text = value,
                Kind = DocumentCellKind.Number,
                NumberValue = number,
                NumberFormat = format,
                SourceIndexes = sourceIndexes,
            };
        }

        return new DocumentCell
        {
            Header = header,
            Text = value,
            Kind = DocumentCellKind.Text,
            SourceIndexes = sourceIndexes,
        };
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var trimmed = value.Trim();
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d.M.yyyy"];
        if (
            DateTime.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date
            )
        )
        {
            return true;
        }

        return DateTime.TryParseExact(
            trimmed,
            ["d MMMM yyyy", "dd MMMM yyyy"],
            Greek,
            DateTimeStyles.None,
            out date
        );
    }

    private static bool TryParseNumber(string value, out double number, out string format)
    {
        number = 0;
        format = "#,##0.00";
        var trimmed = value.Trim();
        var negative = false;

        if (trimmed.StartsWith('(') && trimmed.EndsWith(')'))
        {
            negative = true;
            trimmed = trimmed[1..^1].Trim();
        }

        if (PercentPattern.IsMatch(trimmed))
        {
            var percent = trimmed.Replace("%", string.Empty, StringComparison.Ordinal).Trim();
            if (
                double.TryParse(
                    NormalizeSeparators(percent),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
            )
            {
                number = (negative ? -parsed : parsed) / 100;
                format = "0%";
                return true;
            }

            return false;
        }

        var normalized = trimmed
            .Replace("€", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("£", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (!NumberPattern.IsMatch(normalized))
        {
            return false;
        }

        if (
            !double.TryParse(
                NormalizeSeparators(normalized),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out number
            )
        )
        {
            return false;
        }

        if (negative)
        {
            number = -number;
        }

        return true;
    }

    /// <summary>
    /// Resolves US (1,234.56) and European (1.234,56) separators. When both
    /// appear, the rightmost separator is the decimal separator; a single
    /// comma is treated as thousands only when it groups exactly three digits.
    /// </summary>
    private static string NormalizeSeparators(string value)
    {
        var comma = value.LastIndexOf(',');
        var dot = value.LastIndexOf('.');

        if (comma >= 0 && dot >= 0)
        {
            return dot > comma
                ? value.Replace(",", string.Empty, StringComparison.Ordinal)
                : value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }

        if (comma >= 0)
        {
            return Regex.IsMatch(value, @"^\d{1,3}(,\d{3})+$")
                ? value.Replace(",", string.Empty, StringComparison.Ordinal)
                : value.Replace(',', '.');
        }

        return value;
    }

    private static List<string> SplitParagraphs(string body) =>
        body.Split(
                ["\n\n", "\r\n\r\n"],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .ToList();
}
