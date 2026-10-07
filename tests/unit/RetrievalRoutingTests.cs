using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T011 (granular half): routing through
/// <see cref="RetrievalService"/> keeps the granular child path exactly as
/// today — <c>Auto</c> with a non-trigger query and explicit
/// <c>Specific</c> both return child results with unchanged ordering,
/// <c>MinScore</c> filtering, and <c>topK</c> capping.
/// The broad half of this file arrives with T017.
/// </summary>
public sealed class RetrievalRoutingTests
{
    [Fact]
    public async Task Retrieve_AutoWithFactQuery_ReturnsChildResults()
    {
        var childA = MakeChild("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 0.90f);
        var childB = MakeChild("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", 0.80f);
        var retrieval = GranularRetrieval(new[] { childA, childB });

        var results = await retrieval.RetrieveAsync(
            "What is the renewal term?",
            5,
            QueryMode.Auto
        );

        results.Select(r => r.ChunkId).Should().Equal(childA.ChunkId, childB.ChunkId);
    }

    [Fact]
    public async Task Retrieve_SpecificForcesGranular_EvenForBroadText()
    {
        var child = MakeChild("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 0.90f);
        var retrieval = GranularRetrieval(new[] { child });

        var results = await retrieval.RetrieveAsync(
            "Summarize the onboarding process",
            5,
            QueryMode.Specific
        );

        results.Should().ContainSingle().Which.ChunkId.Should().Be(child.ChunkId);
    }

    [Fact]
    public async Task Retrieve_Granular_KeepsOrderingMinScoreAndTopK()
    {
        var results0 = new[]
        {
            MakeChild("11111111-1111-1111-1111-111111111111", 0.10f),
            MakeChild("22222222-2222-2222-2222-222222222222", 0.90f),
            MakeChild("33333333-3333-3333-3333-333333333333", 0.50f),
        };
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            new ScoringStore(results0),
            Options.Create(new RetrievalOptions { MinScore = 0.25f }),
            NullLogger<RetrievalService>.Instance
        );

        var results = await retrieval.RetrieveAsync("What is the limit?", 5, QueryMode.Auto);

        results.Select(r => r.Score).Should().Equal(0.90f, 0.50f);

        var capped = await retrieval.RetrieveAsync("What is the limit?", 1, QueryMode.Auto);
        capped.Should().ContainSingle().Which.Score.Should().Be(0.90f);
    }

    internal static SearchResult MakeChild(string chunkId, float score, string text = "child text")
    {
        return new SearchResult(
            Guid.Parse(chunkId),
            Guid.NewGuid().ToString(),
            text,
            0,
            score,
            ParentId: Guid.NewGuid().ToString()
        );
    }

    private static RetrievalService GranularRetrieval(IReadOnlyList<SearchResult> results)
    {
        return new RetrievalService(
            new FixedEmbedder(),
            new ScoringStore(results),
            Options.Create(new RetrievalOptions { MinScore = 0.25f }),
            NullLogger<RetrievalService>.Instance
        );
    }

    internal sealed class ScoringStore : IVectorStore
    {
        private readonly IReadOnlyList<SearchResult> _results;

        public ScoringStore(IReadOnlyList<SearchResult> results) => _results = results;

        public Task UpsertAsync(IEnumerable<VectorRecord> vectors, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] q,
            int limit,
            string? f = null,
            CancellationToken ct = default
        ) => Task.FromResult(_results);

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    internal sealed class FixedEmbedder : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(_ => new float[] { 1f, 0f }).ToList()
            );
    }
}
