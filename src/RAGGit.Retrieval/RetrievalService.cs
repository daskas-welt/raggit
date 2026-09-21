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
        ILogger<RetrievalService>? logger
    )
    {
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(vectorStore);
        _embedder = embedder;
        _vectorStore = vectorStore;
        _options = options?.Value ?? new RetrievalOptions();
        _logger = logger ?? NullLogger<RetrievalService>.Instance;
    }

    /// <summary>
    /// Retrieves up to <paramref name="topK"/> relevant chunks for <paramref name="query"/>.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

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
