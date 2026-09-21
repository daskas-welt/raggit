using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// Suggests the closest person actually present in retrieved chunks when the
/// asked name does not match. Strict grounding is preserved: suggestions are
/// surname-anchored hints only, never transferred facts.
/// </summary>
public static class PersonNameSuggester
{
    // Two consecutive capitalized Greek words, with optional "του/της + Name".
    private static readonly Regex NameRegex = new(
        @"\b([Α-ΩΆΈΉΊΌΎΏΪΫ][α-ωάέήίόύώϊϋΐΰς]{2,})\s+([Α-ΩΆΈΉΊΌΎΏΪΫ][α-ωάέήίόύώϊϋΐΰς]{2,})(?:\s+(?:του|της)\s+([Α-ΩΆΈΉΊΌΎΏΪΫ][α-ωάέήίόύώϊϋΐΰς]{2,}))?",
        RegexOptions.Compiled
    );

    private static readonly HashSet<string> Stopwords = new(
        new[]
        {
            "συνολικο",
            "συνολικη",
            "συνολικος",
            "χρονο",
            "χρονου",
            "χρόνος",
            "προηπηρεσια",
            "προηπηρεσιασ",
            "προϋπηρεσια",
            "προϋπηρεσιας",
            "εντοσ",
            "δημοσιο",
            "δημοσιου",
            "δημοσίου",
            "τομεα",
            "τομεασ",
            "τησ",
            "τη",
            "την",
            "το",
            "του",
            "τον",
            "τα",
            "οι",
            "ο",
            "η",
            "και",
            "με",
            "για",
            "απο",
            "από",
            "σε",
            "στο",
            "στη",
            "στον",
            "βρεσ",
            "βρες",
            "δωσε",
            "δειξε",
            "ποιοσ",
            "ποια",
            "ποιο",
            "ειναι",
            "είναι",
            "πως",
            "πώς",
            "τι",
            "ποση",
            "ποσο",
        }.Select(GreekNameNormalizer.Normalize),
        StringComparer.Ordinal
    );

    public static IReadOnlyList<PersonSuggestion> Suggest(
        string query,
        IReadOnlyList<SearchResult> chunks,
        int maxSuggestions = 3
    )
    {
        if (string.IsNullOrWhiteSpace(query) || chunks.Count == 0)
        {
            return Array.Empty<PersonSuggestion>();
        }

        var queryTokens = ExtractQueryTokens(query);
        if (queryTokens.Count == 0)
        {
            return Array.Empty<PersonSuggestion>();
        }

        var candidates = new Dictionary<string, PersonSuggestion>(StringComparer.Ordinal);
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.Text))
            {
                continue;
            }

            foreach (Match match in NameRegex.Matches(chunk.Text))
            {
                var display = match.Value.Trim();
                var normalized = GreekNameNormalizer.Normalize(display);
                var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                var surname = parts[0];
                // Surname-anchored: only suggest when the asked query actually
                // contains the same normalized surname token.
                if (!queryTokens.Contains(surname))
                {
                    continue;
                }

                frequency[normalized] = frequency.TryGetValue(normalized, out var n) ? n + 1 : 1;
                if (!candidates.ContainsKey(normalized))
                {
                    Guid? documentId = Guid.TryParse(chunk.DocumentId, out var id) ? id : null;
                    candidates[normalized] = new PersonSuggestion
                    {
                        Name = display,
                        DocumentId = documentId,
                    };
                }
            }
        }

        return candidates
            .OrderByDescending(kv => frequency[kv.Key])
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(Math.Clamp(maxSuggestions, 1, 5))
            .Select(kv => kv.Value)
            .ToList();
    }

    internal static HashSet<string> ExtractQueryTokens(string query)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(query, @"\p{L}{3,}"))
        {
            var normalized = GreekNameNormalizer.Normalize(match.Value);
            if (normalized.Length < 3 || Stopwords.Contains(normalized))
            {
                continue;
            }

            tokens.Add(normalized);
        }

        return tokens;
    }
}
