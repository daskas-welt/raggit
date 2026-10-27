using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// The single query orchestration both transports use (032 FR-010, research R2):
/// retrieval → source resolution → generation → citation and did-you-mean
/// assembly. Progress is reported through an optional observer — the streaming
/// endpoint writes each update live, the non-streaming endpoint passes null, so
/// the two paths cannot drift.
/// </summary>
public sealed class QueryPipeline
{
    /// <summary>Exact refusal answer required by the citation-grounding invariant.</summary>
    public const string NoRelevantContent = "no relevant content found";

    private readonly RetrievalService _retrievalService;
    private readonly GenerationService _generationService;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<QueryPipeline> _logger;

    /// <summary>Creates the pipeline with no-op logging.</summary>
    public QueryPipeline(
        RetrievalService retrievalService,
        GenerationService generationService,
        IDocumentRepository documents
    )
        : this(retrievalService, generationService, documents, null) { }

    /// <summary>Creates the pipeline with the given logger.</summary>
    public QueryPipeline(
        RetrievalService retrievalService,
        GenerationService generationService,
        IDocumentRepository documents,
        ILogger<QueryPipeline>? logger
    )
    {
        ArgumentNullException.ThrowIfNull(retrievalService);
        ArgumentNullException.ThrowIfNull(generationService);
        ArgumentNullException.ThrowIfNull(documents);

        _retrievalService = retrievalService;
        _generationService = generationService;
        _documents = documents;
        _logger = logger ?? NullLogger<QueryPipeline>.Instance;
    }

    /// <summary>
    /// Runs one query. <paramref name="onProgress"/> is invoked at the moment
    /// each stage's work begins (or, for <see cref="QueryStage.Sources"/>, when
    /// retrieval has produced sources) and may be null.
    /// </summary>
    public async Task<QueryOutcome> ExecuteAsync(
        QueryRequest request,
        Func<QueryProgress, CancellationToken, ValueTask>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var query = request.Query;
        var stopwatch = Stopwatch.StartNew();

        await ReportAsync(
            onProgress,
            QueryStage.Understanding,
            query,
            null,
            null,
            cancellationToken
        );
        await ReportAsync(onProgress, QueryStage.Searching, query, null, null, cancellationToken);

        var chunks = await _retrievalService.RetrieveAsync(
            query,
            Math.Clamp(request.TopK, 1, 5),
            cancellationToken
        );

        if (chunks.Count == 0)
        {
            var noContentLatency = (int)stopwatch.ElapsedMilliseconds;
            _logger.LogInformation(
                "Query returned no relevant content in {LatencyMs}ms",
                noContentLatency
            );

            return new QueryOutcome { Answer = NoRelevantContent, LatencyMs = noContentLatency };
        }

        // Resolved before the sources stage so the employee sees which sources
        // were found — and before generation, exactly as before this feature.
        var documentNames = await ResolveDocumentNamesAsync(chunks, cancellationToken);
        var sourceCount = chunks.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal).Count();
        var sourceNames = DistinctSourceNames(chunks, documentNames);

        await ReportAsync(
            onProgress,
            QueryStage.Sources,
            query,
            sourceCount,
            sourceNames,
            cancellationToken
        );
        await ReportAsync(onProgress, QueryStage.Composing, query, null, null, cancellationToken);

        var (answer, citationIds) = await _generationService.GenerateAsync(
            query,
            chunks,
            documentNames,
            cancellationToken
        );

        var latencyMs = (int)stopwatch.ElapsedMilliseconds;
        var retrievedChunkIds = chunks.Select(c => c.ChunkId).ToList();

        // Strict grounding is preserved: when the LLM cites nothing (e.g.
        // near-miss person name like Αρετής vs Πηνελόπης), offer
        // surname-anchored did-you-mean hints from retrieved chunks only.
        var suggestions = new List<PersonSuggestion>();
        if (citationIds.Count == 0)
        {
            suggestions = PersonNameSuggester.Suggest(query, chunks).ToList();
        }

        var finalAnswer = answer;
        if (suggestions.Count > 0 && AnswerLanguage.IsRefusalLike(answer))
        {
            var names = string.Join("; ", suggestions.Select(s => s.Name));
            finalAnswer = AnswerLanguage.IsGreek(query)
                ? $"{answer} Μήπως εννοούσατε: {names}; Παρακαλώ επιβεβαιώστε."
                : $"{answer} Did you mean: {names}? Please confirm.";
        }

        var citations = chunks
            .Where(c => citationIds.Contains(c.ChunkId))
            .Where(c => Guid.TryParse(c.DocumentId, out _))
            .Select(c => new Citation
            {
                DocumentId = Guid.Parse(c.DocumentId),
                DocumentName = documentNames.GetValueOrDefault(c.DocumentId),
                ChunkId = c.ChunkId,
                Text = c.Text,
                Ordinal = c.Ordinal,
            })
            .ToList();

        return new QueryOutcome
        {
            Answer = finalAnswer,
            Citations = citations,
            CitationIds = citationIds,
            RetrievedChunkIds = retrievedChunkIds,
            LatencyMs = latencyMs,
            SuggestedPersons = suggestions,
        };
    }

    /// <summary>
    /// Filenames of the retrieved documents, in first-appearance order. Unknown
    /// names are dropped: an internal id is never shown (FR-007).
    /// </summary>
    private static List<string> DistinctSourceNames(
        IReadOnlyList<SearchResult> chunks,
        IReadOnlyDictionary<string, string?> documentNames
    ) =>
        chunks
            .Select(c => documentNames.GetValueOrDefault(c.DocumentId))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static async ValueTask ReportAsync(
        Func<QueryProgress, CancellationToken, ValueTask>? onProgress,
        QueryStage stage,
        string query,
        int? sourceCount,
        IReadOnlyList<string>? documentNames,
        CancellationToken cancellationToken
    )
    {
        if (onProgress is null)
        {
            return;
        }

        var progress = new QueryProgress
        {
            Stage = stage,
            Text = ReasoningText.For(stage, query, sourceCount, documentNames),
            SourceCount = stage == QueryStage.Sources ? sourceCount : null,
            DocumentNames = stage == QueryStage.Sources ? documentNames?.ToList() : null,
        };

        await onProgress(progress, cancellationToken);
    }

    private async Task<Dictionary<string, string?>> ResolveDocumentNamesAsync(
        IReadOnlyList<SearchResult> chunks,
        CancellationToken cancellationToken
    )
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var documentId in chunks.Select(c => c.DocumentId).Distinct())
        {
            if (!Guid.TryParse(documentId, out var id))
            {
                continue;
            }

            try
            {
                var document = await _documents.FindByIdAsync(id, cancellationToken);
                names[documentId] = document?.Filename;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "Document name lookup failed for {DocumentId}; using id in prompt",
                    documentId
                );
            }
        }

        return names;
    }
}
