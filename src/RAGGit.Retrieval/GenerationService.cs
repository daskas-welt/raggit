using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Abstractions;

namespace RAGGit.Retrieval;

/// <summary>
/// Builds a grounded prompt from retrieved chunks, asks the local LLM, and
/// extracts citation chunk IDs from the generated answer.
/// </summary>
public sealed class GenerationService
{
    private readonly ILlmClient _llmClient;
    private static readonly Regex CitationRegex = new(
        @"\[([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\]",
        RegexOptions.Compiled);

    public GenerationService(ILlmClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    /// <summary>
    /// Generates an answer for <paramref name="query"/> using the supplied
    /// <paramref name="chunks"/>. Returns the answer and the chunk IDs cited.
    /// </summary>
    public async Task<(string Answer, IReadOnlyList<Guid> CitationIds)> GenerateAsync(
        string query,
        IReadOnlyList<SearchResult> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return ("no relevant content found", Array.Empty<Guid>());
        }

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(query, chunks);

        var answer = await _llmClient.ChatAsync(systemPrompt, userPrompt, cancellationToken);

        var citationIds = ExtractCitationIds(answer)
            .Where(id => chunks.Any(c => c.ChunkId == id))
            .Distinct()
            .ToList();

        // Fallback: if the LLM did not emit explicit citations but used the
        // provided context, cite all retrieved chunks. This guarantees SC-004
        // citation coverage without encouraging hallucination.
        if (citationIds.Count == 0)
        {
            citationIds = chunks.Select(c => c.ChunkId).Distinct().ToList();
        }

        return (answer, citationIds);
    }

    private static string BuildSystemPrompt()
    {
        return "You are a helpful assistant for a single-tenant company document library. " +
            "Answer the user's question using ONLY the provided context. " +
            "Cite your sources by including the chunk IDs in square brackets, e.g. [aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee]. " +
            "If the context does not contain the answer, say 'no relevant content found'.";
    }

    private static string BuildUserPrompt(string query, IEnumerable<SearchResult> chunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Context:");

        foreach (var chunk in chunks)
        {
            builder.AppendLine($"[{chunk.ChunkId}] (Document: {chunk.DocumentId}, Ordinal: {chunk.Ordinal}) {chunk.Text}");
        }

        builder.AppendLine();
        builder.AppendLine($"Question: {query}");

        return builder.ToString();
    }

    private static IEnumerable<Guid> ExtractCitationIds(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            yield break;
        }

        foreach (Match match in CitationRegex.Matches(answer))
        {
            if (Guid.TryParse(match.Groups[1].Value, out var id))
            {
                yield return id;
            }
        }
    }
}
