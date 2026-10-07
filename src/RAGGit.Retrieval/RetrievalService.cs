using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// Embeds a natural-language query and retrieves the top-k most relevant
/// chunks from the configured vector store, ordered by similarity score and
/// filtered by the configured <see cref="RetrievalOptions.MinScore"/>.
/// </summary>
public sealed class RetrievalService
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;
    private readonly RetrievalOptions _options;
    private readonly ILogger<RetrievalService> _logger;
    private readonly IDocumentRepository? _documents;

    /// <summary>
    /// Creates a retrieval service with default options and no-op logging.
    /// </summary>
    public RetrievalService(IEmbedder embedder, IVectorStore vectorStore)
        : this(embedder, vectorStore, null, null) { }

    /// <summary>
    /// Creates a retrieval service with the given options and logger.
    /// </summary>
    public RetrievalService(
        IEmbedder embedder,
        IVectorStore vectorStore,
        IOptions<RetrievalOptions>? options,
        ILogger<RetrievalService>? logger,
        IDocumentRepository? documents = null
    )
    {
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(vectorStore);
        _embedder = embedder;
        _vectorStore = vectorStore;
        _options = options?.Value ?? new RetrievalOptions();
        _logger = logger ?? NullLogger<RetrievalService>.Instance;
        _documents = documents;
    }

    /// <summary>
    /// Resolves the effective intent for <paramref name="query"/>: an
    /// explicit <see cref="QueryMode.Broad"/>/<see cref="QueryMode.Specific"/>
    /// override wins; <see cref="QueryMode.Auto"/> runs the rule-based
    /// classifier (safe default granular).
    /// </summary>
    public static QueryIntent ResolveIntent(string query, QueryMode mode) =>
        mode switch
        {
            QueryMode.Broad => QueryIntent.Broad,
            QueryMode.Specific => QueryIntent.Granular,
            _ => QueryIntentClassifier.Classify(query),
        };

    /// <summary>
    /// Retrieves up to <paramref name="topK"/> relevant chunks for <paramref name="query"/>.
    /// Pre-feature compatibility shim: behaves as <see cref="QueryMode.Auto"/>.
    /// </summary>
    public Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    ) => RetrieveAsync(query, topK, QueryMode.Auto, cancellationToken);

    /// <summary>
    /// Retrieves up to <paramref name="topK"/> relevant chunks for <paramref name="query"/>
    /// at the granularity selected by <paramref name="mode"/>.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK,
        QueryMode mode,
        CancellationToken cancellationToken = default
    )
    {
        var (results, _) = await RetrieveWithIntentAsync(query, topK, mode, cancellationToken);
        return results;
    }

    /// <summary>
    /// Retrieves plus reports the effective intent actually used, so the
    /// controller can echo and persist it.
    /// </summary>
    public async Task<(IReadOnlyList<SearchResult> Results, QueryIntent Intent)> RetrieveWithIntentAsync(
        string query,
        int topK,
        QueryMode mode,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var intent = ResolveIntent(query, mode);
        if (intent == QueryIntent.Broad)
        {
            // US2 (T023) implements parent-context retrieval. Until then a
            // broad request safely falls back to the granular child path
            // rather than failing.
            _logger.LogInformation(
                "Broad intent for query {QueryPreview}: granular fallback until parent retrieval lands (T023)",
                TextPreview.Truncate(query)
            );
        }

        var results = await RetrieveGranularAsync(query, topK, cancellationToken);
        return (results, intent);
    }

    /// <summary>
    /// Granular child path — exactly the pre-feature retrieval: embed the
    /// query, search child vectors, order by score, apply
    /// <see cref="RetrievalOptions.MinScore"/>, take <paramref name="topK"/>.
    /// </summary>
    private async Task<IReadOnlyList<SearchResult>> RetrieveGranularAsync(
        string query,
        int topK,
        CancellationToken cancellationToken
    )
    {
        topK = Math.Clamp(topK, 1, 5);

        var embeddings = await _embedder.GetEmbeddingsAsync(new[] { query }, cancellationToken);
        if (embeddings.Count == 0)
        {
            return Array.Empty<SearchResult>();
        }

        var queryVector = embeddings[0];
        var results = await _vectorStore.SearchAsync(
            queryVector,
            topK,
            cancellationToken: cancellationToken
        );

        // Order by score (vector stores are not contractually ordered) and
        // drop results below the configured similarity floor so the
        // controller can return the "no relevant content found" branch per
        // SC-004 instead of forcing cross-document pollution.
        var minScore = _options.MinScore;
        var filtered = results
            .OrderByDescending(r => r.Score)
            .Where(r => r.Score >= minScore)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "Retrieval for query {QueryPreview}: {Kept}/{Total} chunks above MinScore {MinScore}; top scores [{Scores}]",
            TextPreview.Truncate(query),
            filtered.Count,
            results.Count,
            minScore,
            string.Join(", ", filtered.Take(5).Select(r => r.Score.ToString("0.###")))
        );

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Retrieval chunks: {Chunks}",
                string.Join(
                    " | ",
                    filtered.Select(c =>
                        $"[{c.ChunkId}] score {c.Score:0.###} doc {c.DocumentId} text: {TextPreview.Truncate(c.Text, 120)}"
                    )
                )
            );
        }

        return filtered;
    }
}
