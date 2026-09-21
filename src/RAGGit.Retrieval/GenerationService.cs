using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// Builds a grounded prompt from retrieved chunks, asks the local LLM, and
/// extracts citation chunk IDs from the generated answer.
/// </summary>
public sealed class GenerationService
{
    private readonly ILlmClient _llmClient;
    private readonly GenerationOptions _options;
    private readonly ILogger<GenerationService> _logger;
    private static readonly Regex CitationRegex = new(
        @"\[([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\]",
        RegexOptions.Compiled
    );

    /// <summary>
    /// Creates a generation service with default options and no-op logging.
    /// </summary>
    public GenerationService(ILlmClient llmClient)
        : this(llmClient, null, null) { }

    /// <summary>
    /// Creates a generation service with the given options and logger.
    /// </summary>
    public GenerationService(
        ILlmClient llmClient,
        IOptions<GenerationOptions>? options,
        ILogger<GenerationService>? logger
    )
    {
        ArgumentNullException.ThrowIfNull(llmClient);
        _llmClient = llmClient;
        _options = options?.Value ?? new GenerationOptions();
        _logger = logger ?? NullLogger<GenerationService>.Instance;
    }

    /// <summary>
    /// Generates an answer for <paramref name="query"/> using the supplied
    /// <paramref name="chunks"/> (expected pre-sorted by score, best first).
    /// Returns the answer and the chunk IDs cited.
    /// </summary>
    public Task<(string Answer, IReadOnlyList<Guid> CitationIds)> GenerateAsync(
        string query,
        IReadOnlyList<SearchResult> chunks,
        CancellationToken cancellationToken = default
    ) => GenerateAsync(query, chunks, null, cancellationToken);

    /// <summary>
    /// Generates an answer with human-readable document names available for
    /// the prompt. Keys are <see cref="SearchResult.DocumentId"/> strings,
    /// values are filenames (null when unknown — the raw id is used).
    /// </summary>
    public async Task<(string Answer, IReadOnlyList<Guid> CitationIds)> GenerateAsync(
        string query,
        IReadOnlyList<SearchResult> chunks,
        IReadOnlyDictionary<string, string?>? documentNames,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return ("no relevant content found", Array.Empty<Guid>());
        }

        var systemPrompt = BuildSystemPrompt(query);
        var userPrompt = BuildUserPrompt(query, chunks, documentNames);

        _logger.LogDebug(
            "Generation prompt for query {QueryPreview}: {ChunkCount} chunks",
            TextPreview.Truncate(query),
            chunks.Count
        );

        var answer = await _llmClient.ChatAsync(systemPrompt, userPrompt, cancellationToken);

        // Same-language enforcement: small local models often answer in
        // English even for Greek queries. Retry once with an explicit
        // instruction instead of returning a wrong-language answer.
        if (!AnswerLanguage.MatchesQueryLanguage(query, answer))
        {
            _logger.LogInformation(
                "Generation language retry: query is Greek but answer has no Greek script; retrying once"
            );
            answer = await _llmClient.ChatAsync(
                systemPrompt
                    + " REMINDER: the question is in Greek. You MUST answer in Greek, not in English.",
                userPrompt,
                cancellationToken
            );
        }

        var citationIds = ExtractCitationIds(answer)
            .Where(id => chunks.Any(c => c.ChunkId == id))
            .Distinct()
            .ToList();

        // Fallback: small local models often fail to echo full GUIDs. Citing
        // every retrieved chunk guaranteed citations to irrelevant documents
        // (e.g. same-template records about a different person), so cite at
        // most the single best chunk — and only when it clears
        // FallbackMinScore. Below that floor even the best chunk is too weak
        // to vouch for, so no fallback citation is emitted. Refusals are
        // never cited: attaching a chunk to 'no relevant content found'
        // breaks the prompt contract and suppresses did-you-mean handling.
        if (citationIds.Count == 0 && !AnswerLanguage.IsRefusalLike(answer))
        {
            var top = chunks.OrderByDescending(c => c.Score).First();
            if (_options.FallbackToTopChunk && top.Score >= _options.FallbackMinScore)
            {
                citationIds = new List<Guid> { top.ChunkId };
                _logger.LogInformation(
                    "Generation fallback: LLM emitted no citations; citing top chunk {ChunkId} (score {Score}) only",
                    top.ChunkId,
                    top.Score
                );
            }
            else
            {
                _logger.LogInformation(
                    "Generation strict: LLM emitted no citations and top score {Score} is below FallbackMinScore {Floor}; returning zero citations",
                    top.Score,
                    _options.FallbackMinScore
                );
            }
        }

        return (answer, citationIds);
    }

    private static string BuildSystemPrompt(string query)
    {
        var languageRule = AnswerLanguage.IsGreek(query)
            ? "CRITICAL: the question is in Greek. You MUST answer in Greek, not in English. "
            : "The question is in English. Answer in English. ";
        return "You are a helpful assistant for a single-tenant company document library. "
            + languageRule
            + "Answer the user's question using ONLY the provided context. "
            + "Each context chunk shows its chunk ID, document, and ordinal. "
            + "When the question names a person, use ONLY chunks that mention that same person; "
            + "ignore same-template chunks about other people. "
            + "If no chunk mentions the asked person, do not transfer facts from another person. "
            + "Instead state the asked name, list the distinct person names actually present in the context "
            + "as possible intended persons, and ask the user to confirm. "
            + "Cite ONLY chunks that directly support your answer by including their chunk IDs in square brackets, e.g. [aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee]. "
            + "Cite at most the chunks you actually used, never the whole list. "
            + "Answer in the same language as the question (e.g. Greek when asked in Greek). "
            + "If the context does not contain the answer, say exactly 'no relevant content found' with no citations.";
    }

    private static string BuildUserPrompt(
        string query,
        IEnumerable<SearchResult> chunks,
        IReadOnlyDictionary<string, string?>? documentNames
    )
    {
        var builder = new StringBuilder();
        builder.AppendLine("Context:");

        foreach (var chunk in chunks)
        {
            var docLabel = chunk.DocumentId;
            if (
                documentNames is not null
                && documentNames.TryGetValue(chunk.DocumentId, out var name)
                && !string.IsNullOrWhiteSpace(name)
            )
            {
                docLabel = $"{name} ({chunk.DocumentId})";
            }

            builder.AppendLine(
                $"[{chunk.ChunkId}] (Document: {docLabel}, Ordinal: {chunk.Ordinal})"
            );
            builder.AppendLine(chunk.Text);
        }

        builder.AppendLine();
        var questionLanguage = AnswerLanguage.IsGreek(query) ? "Greek" : "English";
        builder.AppendLine(
            $"Question ({questionLanguage} - answer in {questionLanguage}): {query}"
        );
        builder.AppendLine(
            "Important: if the question is about a named person, answer only from chunks about that person."
        );

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
