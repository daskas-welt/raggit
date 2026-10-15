using System;
using System.Collections.Generic;
using System.Linq;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// Renders the employee-facing sentence for each reasoning stage (032 FR-002/
/// FR-006). Sentences follow the question's language — the same detection the
/// answer language rule uses — so narration and answer can never disagree, and
/// they carry no prompt text, chunk body, identifier, or configuration name.
/// </summary>
public static class ReasoningText
{
    /// <summary>
    /// Plain-language sentence for <paramref name="stage"/> in the question's
    /// language. <paramref name="sourceCount"/>/<paramref name="documentNames"/>
    /// are meaningful only for <see cref="QueryStage.Sources"/>.
    /// </summary>
    public static string For(
        QueryStage stage,
        string query,
        int? sourceCount = null,
        IReadOnlyList<string>? documentNames = null
    )
    {
        var greek = AnswerLanguage.IsGreek(query);

        return stage switch
        {
            QueryStage.Understanding => greek
                ? "Διαβάζω την ερώτησή σας…"
                : "Reading your question…",
            QueryStage.Searching => greek ? "Αναζητώ στη βιβλιοθήκη…" : "Searching the library…",
            QueryStage.Sources => SourcesText(greek, sourceCount, documentNames),
            QueryStage.Composing => greek ? "Συντάσσω την απάντηση…" : "Writing your answer…",
            _ => greek ? "Επεξεργάζομαι την ερώτησή σας…" : "Working on your question…",
        };
    }

    private static string SourcesText(
        bool greek,
        int? sourceCount,
        IReadOnlyList<string>? documentNames
    )
    {
        var count = sourceCount ?? 0;
        if (count <= 0)
        {
            // The pipeline never emits this stage without sources (FR-005);
            // returning a truthful sentence keeps the text from ever claiming
            // a find it cannot back.
            return greek ? "Δεν βρέθηκαν σχετικές πηγές." : "No relevant sources were found.";
        }

        // Unknown filenames are omitted rather than replaced by an internal id
        // (FR-007): the sentence then reports the count alone.
        var names = (documentNames ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var head = greek
            ? count == 1
                ? "Βρέθηκε 1 σχετική πηγή"
                : $"Βρέθηκαν {count} σχετικές πηγές"
            : count == 1
                ? "Found 1 relevant source"
                : $"Found {count} relevant sources";

        return names.Count == 0 ? head : $"{head}: {string.Join(", ", names)}";
    }
}
