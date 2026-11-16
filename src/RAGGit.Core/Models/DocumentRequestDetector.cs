using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RAGGit.Core.Models;

/// <summary>
/// Deterministic, conservative reader for "make me a document" wording (033
/// FR-002/FR-007/FR-024). A supported format is returned only when exactly one
/// format and a document-creation intent are present; ordinary questions are
/// never turned into files.
/// </summary>
public static class DocumentRequestDetector
{
    private static readonly (DocumentOutputFormat Format, string[] Tokens)[] SupportedFormats =
    [
        (DocumentOutputFormat.Docx, ["word", "docx"]),
        (DocumentOutputFormat.Xlsx, ["excel", "xlsx", "spreadsheet", "υπολογιστικό φύλλο"]),
        (DocumentOutputFormat.Pdf, ["pdf"]),
        (DocumentOutputFormat.Txt, ["text", "txt", "plain text", "κείμενο", "αρχείο κειμένου"]),
    ];

    private static readonly string[] IntentTokens =
    [
        "create",
        "make",
        "write",
        "generate",
        "export",
        "save",
        "produce",
        "put",
        "draft",
        "build",
        "need",
        "want",
        "give me",
        "δημιούργησε",
        "δημιουργία",
        "φτιάξε",
        "φτιάχνω",
        "γράψε",
        "γράψιμο",
        "εξήγαγε",
        "εξαγωγή",
        "αποθήκευσε",
        "αποθήκευση",
        "θέλω",
        "χρειάζομαι",
        "δώσε",
    ];

    private static readonly (string Name, string[] Tokens)[] UnsupportedFormats =
    [
        ("PowerPoint", ["powerpoint", "pptx", "presentation"]),
        ("image", ["image", "photo", "picture"]),
        ("email", ["email", "e-mail", "outlook"]),
    ];

    /// <summary>Reads one prompt; never throws for null/blank input.</summary>
    public static DocumentFormatDetection Detect(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new DocumentFormatDetection();
        }

        var text = prompt.Trim();
        if (!HasAny(text, IntentTokens))
        {
            return new DocumentFormatDetection();
        }

        var formats = SupportedFormats
            .Where(candidate => HasAny(text, candidate.Tokens))
            .Select(candidate => candidate.Format)
            .Distinct()
            .ToList();

        if (formats.Count > 1)
        {
            return new DocumentFormatDetection { IsAmbiguous = true };
        }

        if (formats.Count == 1)
        {
            return new DocumentFormatDetection { Format = formats[0] };
        }

        var unsupported = UnsupportedFormats.FirstOrDefault(candidate =>
            HasAny(text, candidate.Tokens)
        );
        return unsupported.Name is null
            ? new DocumentFormatDetection()
            : new DocumentFormatDetection { UnsupportedFormat = unsupported.Name };
    }

    private static bool HasAny(string text, IEnumerable<string> tokens) =>
        tokens.Any(token => HasToken(text, token));

    private static bool HasToken(string text, string token) =>
        Regex.IsMatch(
            text,
            $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(token)}(?![\\p{{L}}\\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
}
