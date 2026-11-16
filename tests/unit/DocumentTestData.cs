using System;
using System.Collections.Generic;
using RAGGit.Core.Models;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Builders for document tests: a persisted query detail with a grounded answer
/// and ordered citations, in English or Greek.
/// </summary>
internal static class DocumentTestData
{
    public static HistoryCitation Citation(
        Guid? chunkId = null,
        string documentName = "employee-record.pdf",
        int ordinal = 1,
        string text = "Maria Papadopoulou was hired in 2019."
    ) =>
        new()
        {
            DocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DocumentName = documentName,
            ChunkId = chunkId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Text = text,
            Ordinal = ordinal,
        };

    public static QueryDetail Detail(
        string answer =
            "Maria Papadopoulou was hired in 2019 [22222222-2222-2222-2222-222222222222].",
        string prompt = "Create a Word document with the employment history of Maria Papadopoulou.",
        IReadOnlyList<HistoryCitation>? citations = null
    ) =>
        new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Prompt = prompt,
            Answer = answer,
            Citations = new List<HistoryCitation>(citations ?? new[] { Citation() }),
            LatencyMs = 1200,
            CreatedAt = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc),
        };

    public static QueryDetail GreekDetail() =>
        Detail(
            answer: "Η Μαρία Παπαδοπούλου προσλήφθηκε το 2019 [22222222-2222-2222-2222-222222222222].",
            prompt: "Δημιούργησε ένα έγγραφο Word με το ιστορικό απασχόλησης της Μαρίας Παπαδοπούλου.",
            citations: new[] { Citation(text: "Η Μαρία Παπαδοπούλου προσλήφθηκε το 2019.") }
        );

    public static QueryDetail RecordDetail() =>
        Detail(
            answer: "Name: Maria Papadopoulou\nRole: Analyst\nStart date: 01/09/2019\n\nName: Giorgos Nikolaou\nRole: Manager\nMissing field: (empty)\n",
            prompt: "Create an Excel spreadsheet listing the employees.",
            citations: new[]
            {
                Citation(text: "Maria Papadopoulou — Analyst — 01/09/2019", ordinal: 1),
                Citation(
                    chunkId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    documentName: "employees.xlsx",
                    ordinal: 2,
                    text: "Giorgos Nikolaou — Manager"
                ),
            }
        );

    public static QueryDetail RefusalDetail() =>
        Detail(
            answer: "no relevant content found",
            prompt: "Create a Word document about an unknown subject.",
            citations: Array.Empty<HistoryCitation>()
        );
}
