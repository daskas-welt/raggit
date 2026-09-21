using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using RAGGit.Core.Abstractions;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Greek-language handling: the embedding cache normalizer must fold the
/// Greek final sigma (ς) to medial sigma (σ) so the same word caches
/// identically in capitalized (Σ→σ) and lowercase (ς) forms.
/// </summary>
public sealed class GreekLanguageTests
{
    [Fact]
    public async Task CachedEmbedder_FoldsFinalSigma_SecondCallHitsCache()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var inner = new CountingEmbedder();
        var embedder = new CachedEmbedder(inner, cache, "test-model", 100, 24);

        await embedder.GetEmbeddingsAsync(new[] { "ΟΔΥΣΣΕΑΣ" });
        await embedder.GetEmbeddingsAsync(new[] { "οδυσσεας" });

        inner.CallCount.Should().Be(1, "ΟΔΥΣΣΕΑΣ and οδυσσεας must share a cache key");
    }

    [Fact]
    public async Task CachedEmbedder_DistinctGreekWords_DoNotCollide()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        var inner = new CountingEmbedder();
        var embedder = new CachedEmbedder(inner, cache, "test-model", 100, 24);

        await embedder.GetEmbeddingsAsync(new[] { "Μαρία" });
        await embedder.GetEmbeddingsAsync(new[] { "Μαρίος" });

        inner.CallCount.Should().Be(2);
    }

    private sealed class CountingEmbedder : IEmbedder
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            var result = new List<float[]>();
            foreach (var _ in inputs)
            {
                result.Add(new float[] { 1f });
            }

            return Task.FromResult<IReadOnlyList<float[]>>(result);
        }
    }
}
