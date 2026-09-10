using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Abstractions;

namespace RAGGit.Retrieval;

/// <summary>
/// Embeds a natural-language query and retrieves the top-k most relevant
/// chunks from the configured vector store.
/// </summary>
public sealed class RetrievalService
{
    private readonly IEmbedder _embedder;
    private readonly IVectorStore _vectorStore;

    public RetrievalService(IEmbedder embedder, IVectorStore vectorStore)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    /// <summary>
    /// Retrieves up to <paramref name="topK"/> relevant chunks for <paramref name="query"/>.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        topK = Math.Clamp(topK, 1, 5);

        var embeddings = await _embedder.GetEmbeddingsAsync(new[] { query }, cancellationToken);
        if (embeddings.Count == 0)
        {
            return Array.Empty<SearchResult>();
        }

        var queryVector = embeddings[0];
        var results = await _vectorStore.SearchAsync(queryVector, topK, cancellationToken: cancellationToken);

        // Drop results with no meaningful similarity so the controller can
        // return the "no relevant content found" branch per SC-004.
        const float minScore = 0.01f;
        return results.Where(r => r.Score > minScore).ToList();
    }
}
