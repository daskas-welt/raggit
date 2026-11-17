using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>
/// Derives a readable title and an OS-safe suggested filename from the request
/// wording (033 FR-004/FR-006). The employee can always rename in the save dialog.
/// </summary>
public static class DocumentFileName
{
    private static readonly Regex LeadingCommand = new(
        @"^\s*(please\s+)?(create|make|write|generate|export|save|produce|draft|build|δημιούργησε|φτιάξε|γράψε|εξήγαγε|αποθήκευσε|θέλω|χρειάζομαι|δώσε)\b\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex FormatWords = new(
        @"\b(word|docx|excel|xlsx|spreadsheet|pdf|txt|text|plain\s+text|έγγραφο\s+word|υπολογιστικό\s+φύλλο|κείμενο|αρχείο\s+κειμένου)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex DocumentWords = new(
        @"\b(document|file|έγγραφο|αρχείο)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex LeadingFiller = new(
        @"^\s*(a|an|the|about|with|for|of|ένα|μία|την|το|τον|με|για|σχετικά\s+με)\b\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private const int MaxTitleLength = 80;

    /// <summary>Readable title with command/format/document words removed.</summary>
    public static string Title(string? prompt)
    {
        var title = (prompt ?? string.Empty).Trim();

        for (var pass = 0; pass < 8; pass++)
        {
            var next = LeadingCommand.Replace(title, string.Empty);
            next = FormatWords.Replace(next, " ");
            next = DocumentWords.Replace(next, " ");
            next = LeadingFiller.Replace(next, string.Empty);
            next = Whitespace.Replace(next, " ").Trim(' ', '.', ',', ';', ':', '-', '—', '–');

            if (string.Equals(next, title, StringComparison.Ordinal))
            {
                break;
            }

            title = next;
        }

        return string.IsNullOrWhiteSpace(title) ? "document" : title;
    }

    /// <summary>Suggested filename with the format's extension; never a path.</summary>
    public static string Create(string? prompt, DocumentOutputFormat format)
    {
        var title = Sanitize(Title(prompt));
        if (title.Length > MaxTitleLength)
        {
            title = title[..MaxTitleLength].TrimEnd();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = "document";
        }

        return title + format.Extension();
    }

    private static string Sanitize(string value)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars())
        {
            '<',
            '>',
            ':',
            '"',
            '/',
            '\\',
            '|',
            '?',
            '*',
        };

        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return Whitespace.Replace(new string(chars), " ").Trim(' ', '.');
    }
}

/// <summary>Greek/English headings introduced by the renderers.</summary>
public sealed class DocumentLabels
{
    public string Title { get; init; } = "Title";
    public string Sources { get; init; } = "Sources";
    public string Page { get; init; } = "Page";
    public string Document { get; init; } = "Document";
    public string NoSources { get; init; } = "No RAGGit sources were used.";

    public static DocumentLabels For(bool isGreek) =>
        isGreek
            ? new DocumentLabels
            {
                Title = "Τίτλος",
                Sources = "Πηγές",
                Page = "Σελίδα",
                Document = "Έγγραφο",
                NoSources = "Δεν χρησιμοποιήθηκαν πηγές από το RAGGit.",
            }
            : new DocumentLabels();
}

/// <summary>
/// Replaces the answer's citation GUIDs with stable 1-based references and
/// builds the sources appendix. Unknown GUID-like text is left untouched.
/// </summary>
public static class DocumentCitationMapper
{
    private static readonly Regex GuidPattern = new(
        @"\[?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]?",
        RegexOptions.Compiled
    );

    public static DocumentCitationMap Map(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return Map(DocumentDraft.FromQueryDetail(detail));
    }

    /// <summary>
    /// Replaces known citation GUIDs with numbered references. An unknown GUID
    /// is removed so a generated document can never show an unverified source.
    /// </summary>
    public static DocumentCitationMap Map(DocumentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var byChunk = new Dictionary<Guid, Citation>();
        foreach (var citation in draft.Sources)
        {
            byChunk.TryAdd(citation.ChunkId, citation);
        }

        var assigned = new Dictionary<Guid, DocumentSource>();
        var sources = new List<DocumentSource>();

        var body = GuidPattern.Replace(
            draft.Body,
            match =>
            {
                if (
                    !Guid.TryParse(match.Value.Trim('[', ']'), out var id)
                    || !byChunk.TryGetValue(id, out var citation)
                )
                {
                    return string.Empty;
                }

                if (!assigned.TryGetValue(id, out var source))
                {
                    source = ToSource(citation, sources.Count + 1);
                    assigned[id] = source;
                    sources.Add(source);
                }

                return $"[{source.Index}]";
            }
        );

        // Include sources the model did not echo in the body so the appendix
        // lists every source the pipeline validated as used.
        foreach (var citation in draft.Sources)
        {
            if (!assigned.ContainsKey(citation.ChunkId))
            {
                var source = ToSource(citation, sources.Count + 1);
                assigned[citation.ChunkId] = source;
                sources.Add(source);
            }
        }

        return new DocumentCitationMap { Body = body, Sources = sources };
    }

    private static DocumentSource ToSource(Citation citation, int index) =>
        new()
        {
            Index = index,
            DocumentId = citation.DocumentId,
            DocumentName = citation.DocumentName,
            ChunkId = citation.ChunkId,
            Ordinal = citation.Ordinal,
            Excerpt = citation.Text,
        };
}
