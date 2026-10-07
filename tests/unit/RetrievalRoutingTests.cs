using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
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

    [Fact]
    public async Task Retrieve_Broad_GroupsChildrenIntoParents_BestScoreWins()
    {
        var documentId = Guid.NewGuid();
        var parentA = Guid.NewGuid();
        var parentB = Guid.NewGuid();
        var store = new ScoringStore(
            new[]
            {
                ChildHit("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", documentId, parentA, 0.90f),
                ChildHit("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", documentId, parentA, 0.60f),
                ChildHit("cccccccc-cccc-cccc-cccc-cccccccccccc", documentId, parentB, 0.80f),
            }
        );
        var repository = new FakeDocumentRepository(
            ParentChunk(parentA, documentId, 0, "parent A text"),
            ParentChunk(parentB, documentId, 1, "parent B text")
        );
        var retrieval = BroadRetrieval(store, repository);

        var results = await retrieval.RetrieveAsync(
            "Summarize the onboarding process",
            5,
            QueryMode.Auto
        );

        results.Should().HaveCount(2);
        results[0].ChunkId.Should().Be(parentA);
        results[0].Score.Should().Be(0.90f);
        results[0].Text.Should().Be("parent A text");
        results[1].ChunkId.Should().Be(parentB);
        results[1].Score.Should().Be(0.80f);
    }

    [Fact]
    public async Task Retrieve_Broad_AppliesMinScoreAndTopKParentCap()
    {
        var documentId = Guid.NewGuid();
        var parentA = Guid.NewGuid();
        var parentB = Guid.NewGuid();
        var parentC = Guid.NewGuid();
        var store = new ScoringStore(
            new[]
            {
                ChildHit("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", documentId, parentA, 0.90f),
                ChildHit("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", documentId, parentB, 0.80f),
                ChildHit("cccccccc-cccc-cccc-cccc-cccccccccccc", documentId, parentC, 0.10f),
            }
        );
        var repository = new FakeDocumentRepository(
            ParentChunk(parentA, documentId, 0, "A"),
            ParentChunk(parentB, documentId, 1, "B"),
            ParentChunk(parentC, documentId, 2, "C")
        );
        var retrieval = BroadRetrieval(store, repository);

        // Parent C's best score (0.10) is below MinScore (0.25) → dropped.
        // topK = 1 → only the best parent.
        var results = await retrieval.RetrieveAsync(
            "Give me an overview",
            1,
            QueryMode.Broad
        );

        results.Should().ContainSingle().Which.ChunkId.Should().Be(parentA);
    }

    [Fact]
    public async Task Retrieve_Broad_UsesWideCandidateLimit()
    {
        var store = new CapturingStore(Array.Empty<SearchResult>());
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            store,
            Options.Create(
                new RetrievalOptions
                {
                    MinScore = 0.0f,
                    BroadCandidateMultiplier = 4,
                    MaxCandidates = 50,
                }
            ),
            NullLogger<RetrievalService>.Instance,
            new FakeDocumentRepository()
        );

        await retrieval.RetrieveAsync("Compare the plans", 5, QueryMode.Broad);

        // min(topK × multiplier, max) = min(20, 50) = 20.
        store.LastLimit.Should().Be(20);
    }

    [Fact]
    public async Task Retrieve_Broad_CapsCandidatesAtMaxCandidates()
    {
        var store = new CapturingStore(Array.Empty<SearchResult>());
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            store,
            Options.Create(
                new RetrievalOptions
                {
                    MinScore = 0.0f,
                    BroadCandidateMultiplier = 100,
                    MaxCandidates = 50,
                }
            ),
            NullLogger<RetrievalService>.Instance,
            new FakeDocumentRepository()
        );

        await retrieval.RetrieveAsync("Compare the plans", 5, QueryMode.Broad);

        store.LastLimit.Should().Be(50);
    }

    [Fact]
    public async Task Retrieve_Broad_OrphanChildWithoutParent_ReturnsChildAsIs()
    {
        var orphan = new SearchResult(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.NewGuid().ToString(),
            "legacy child",
            0,
            0.90f
        );
        var retrieval = BroadRetrieval(
            new ScoringStore(new[] { orphan }),
            new FakeDocumentRepository()
        );

        var results = await retrieval.RetrieveAsync("Summarize everything", 5, QueryMode.Broad);

        results.Should().ContainSingle().Which.ChunkId.Should().Be(orphan.ChunkId);
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

    internal static SearchResult ChildHit(
        string chunkId,
        Guid documentId,
        Guid parentId,
        float score
    )
    {
        return new SearchResult(
            Guid.Parse(chunkId),
            documentId.ToString(),
            "child text",
            0,
            score,
            ParentId: parentId.ToString()
        );
    }

    internal static Chunk ParentChunk(Guid id, Guid documentId, int ordinal, string text)
    {
        return new Chunk
        {
            Id = id,
            DocumentId = documentId,
            Ordinal = ordinal,
            Text = text,
            TokenCount = text.Length,
            Level = ChunkLevel.Parent,
            ParentId = null,
        };
    }

    private static RetrievalService BroadRetrieval(
        IVectorStore store,
        FakeDocumentRepository repository
    )
    {
        return new RetrievalService(
            new FixedEmbedder(),
            store,
            Options.Create(new RetrievalOptions { MinScore = 0.25f }),
            NullLogger<RetrievalService>.Instance,
            repository
        );
    }

    internal sealed class CapturingStore : IVectorStore
    {
        private readonly IReadOnlyList<SearchResult> _results;

        public int LastLimit { get; private set; }

        public CapturingStore(IReadOnlyList<SearchResult> results) => _results = results;

        public Task UpsertAsync(IEnumerable<VectorRecord> vectors, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] q,
            int limit,
            string? f = null,
            CancellationToken ct = default
        )
        {
            LastLimit = limit;
            return Task.FromResult(_results);
        }

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    internal sealed class FakeDocumentRepository : IDocumentRepository
    {
        private readonly Dictionary<Guid, Chunk> _chunks;

        public FakeDocumentRepository(params Chunk[] chunks) =>
            _chunks = chunks.ToDictionary(c => c.Id);

        public Task<DocumentsMinePage> ListMineAsync(
            string sub,
            int? limit,
            int? offset,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<Document?> FindByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Document>> ListAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<Document?> FindByHashAsync(
            string hash,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task AddAsync(Document document, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task AddChunksAsync(
            IEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task DeleteChunksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Chunk>> GetChunksByIdsAsync(
            IEnumerable<Guid> chunkIds,
            CancellationToken cancellationToken = default
        )
        {
            var found = chunkIds
                .Distinct()
                .Where(id => _chunks.ContainsKey(id))
                .Select(id => _chunks[id])
                .ToList();
            return Task.FromResult<IReadOnlyList<Chunk>>(found);
        }

        public Task<IReadOnlyList<Guid>> ListReadyDocumentIdsWithoutParentChunkAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task UpdateStatusAsync(
            Guid documentId,
            DocumentStatus status,
            string? failureReason = null,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<bool> DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();
    }
}
