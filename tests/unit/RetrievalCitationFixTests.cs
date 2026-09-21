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
/// Regression tests for the retrieval + citation fix: configurable MinScore
/// filtering, top-1 (not cite-all) fallback, explicit-citation passthrough,
/// and person-disambiguating prompts with document filenames.
/// </summary>
public sealed class RetrievalCitationFixTests
{
    [Fact]
    public async Task Retrieve_FiltersBelowMinScore_AndOrdersByScoreDesc()
    {
        var store = new ScoringStore(
            new[]
            {
                MakeResult("11111111-1111-1111-1111-111111111111", 0.10f),
                MakeResult("22222222-2222-2222-2222-222222222222", 0.90f),
                MakeResult("33333333-3333-3333-3333-333333333333", 0.50f),
            }
        );
        var embedder = new FixedEmbedder();
        var options = Options.Create(new RetrievalOptions { MinScore = 0.25f });
        var retrieval = new RetrievalService(
            embedder,
            store,
            options,
            NullLogger<RetrievalService>.Instance
        );

        var results = await retrieval.RetrieveAsync("What did Maria do?", 5);

        results.Select(r => r.Score).Should().Equal(0.90f, 0.50f);
        results.Should().OnlyContain(r => r.Score >= 0.25f);
    }

    [Fact]
    public async Task Retrieve_DefaultOptions_FiltersNearZeroScores()
    {
        var store = new ScoringStore(
            new[] { MakeResult("11111111-1111-1111-1111-111111111111", 0.005f) }
        );
        var retrieval = new RetrievalService(new FixedEmbedder(), store);

        var results = await retrieval.RetrieveAsync("hello", 5);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_NoExplicitCitations_CitesTopChunkOnly()
    {
        var chunks = new List<SearchResult>
        {
            MakeResult(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                0.95f,
                "Maria Schmidt works in accounting."
            ),
            MakeResult(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                0.80f,
                "Mario Schmidt works in sales."
            ),
            MakeResult(
                "cccccccc-cccc-cccc-cccc-cccccccccccc",
                0.70f,
                "Marina Schmidt works in support."
            ),
        };
        var llm = new FixedLlm("Maria works in accounting.");
        var generation = new GenerationService(
            llm,
            Options.Create(new GenerationOptions { FallbackToTopChunk = true }),
            NullLogger<GenerationService>.Instance
        );

        var (answer, citationIds) = await generation.GenerateAsync("What does Maria do?", chunks);

        answer.Should().Contain("accounting");
        citationIds.Should().HaveCount(1);
        citationIds[0].Should().Be(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    }

    [Fact]
    public async Task Generate_FallbackSuppressed_WhenTopScoreBelowFloor()
    {
        var chunks = new List<SearchResult>
        {
            MakeResult(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                0.30f,
                "Maria Schmidt works in accounting."
            ),
            MakeResult(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                0.20f,
                "Mario Schmidt works in sales."
            ),
        };
        var llm = new FixedLlm("Maria works in accounting.");
        var generation = new GenerationService(
            llm,
            Options.Create(
                new GenerationOptions { FallbackToTopChunk = true, FallbackMinScore = 0.5f }
            ),
            NullLogger<GenerationService>.Instance
        );

        var (_, citationIds) = await generation.GenerateAsync("What does Maria do?", chunks);

        citationIds.Should().BeEmpty("the best chunk is too weak to vouch for");
    }

    [Fact]
    public async Task Generate_Fallback_PicksHighestScoreRegardlessOfOrder()
    {
        var best = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var chunks = new List<SearchResult>
        {
            MakeResult(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                0.60f,
                "Mario Schmidt works in sales."
            ),
            new(best, Guid.NewGuid().ToString(), "Maria Schmidt works in accounting.", 0, 0.95f),
        };
        var llm = new FixedLlm("Maria works in accounting.");
        var generation = new GenerationService(llm);

        var (_, citationIds) = await generation.GenerateAsync("What does Maria do?", chunks);

        citationIds.Should().Equal(best);
    }

    [Fact]
    public async Task Generate_ExplicitMariaCitation_NeverCitesMario()
    {
        var maria = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var mario = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var marina = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var chunks = new List<SearchResult>
        {
            new(maria, Guid.NewGuid().ToString(), "Maria Schmidt works in accounting.", 0, 0.95f),
            new(mario, Guid.NewGuid().ToString(), "Mario Schmidt works in sales.", 0, 0.90f),
            new(marina, Guid.NewGuid().ToString(), "Marina Schmidt works in support.", 0, 0.85f),
        };
        var llm = new FixedLlm($"Maria Schmidt works in accounting. [{maria}]");
        var generation = new GenerationService(llm);

        var (_, citationIds) = await generation.GenerateAsync(
            "What does Maria Schmidt do?",
            chunks
        );

        citationIds.Should().Equal(maria);
        citationIds.Should().NotContain(mario);
        citationIds.Should().NotContain(marina);
    }

    [Fact]
    public async Task Generate_StrictMode_ReturnsZeroCitations()
    {
        var chunks = new List<SearchResult>
        {
            MakeResult("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", 0.95f, "Maria works in accounting."),
        };
        var llm = new FixedLlm("Maria works in accounting.");
        var generation = new GenerationService(
            llm,
            Options.Create(new GenerationOptions { FallbackToTopChunk = false }),
            NullLogger<GenerationService>.Instance
        );

        var (_, citationIds) = await generation.GenerateAsync("What does Maria do?", chunks);

        citationIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_ExplicitCitation_PassthroughOnlyCited()
    {
        var maria = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var mario = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var chunks = new List<SearchResult>
        {
            new(maria, Guid.NewGuid().ToString(), "Maria works in accounting.", 0, 0.95f),
            new(mario, Guid.NewGuid().ToString(), "Mario works in sales.", 0, 0.80f),
        };
        var llm = new FixedLlm($"Maria works in accounting. [{maria}]");
        var generation = new GenerationService(llm);

        var (_, citationIds) = await generation.GenerateAsync("What does Maria do?", chunks);

        citationIds.Should().Equal(maria);
    }

    [Fact]
    public async Task Generate_RefusalAnswer_NeverGetsFallbackCitation()
    {
        var chunks = new List<SearchResult>
        {
            MakeResult(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                0.95f,
                "Δημοπούλου Πηνελόπη του Σωτηρίου: δύο μήνες."
            ),
        };
        var llm = new FixedLlm("no relevant content found");
        var generation = new GenerationService(
            llm,
            Options.Create(new GenerationOptions { FallbackToTopChunk = true }),
            NullLogger<GenerationService>.Instance
        );

        var (answer, citationIds) = await generation.GenerateAsync(
            "What is the service time of Δημοπούλου Αρετής;",
            chunks
        );

        answer.Should().Be("no relevant content found");
        citationIds.Should().BeEmpty("refusals must carry no citations");
    }

    [Fact]
    public async Task Generate_Prompt_ContainsPersonRuleAndFilename()
    {
        var chunkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var docId = Guid.NewGuid().ToString();
        var chunks = new List<SearchResult>
        {
            new(chunkId, docId, "Maria Schmidt works in accounting.", 0, 0.95f),
        };
        var llm = new CapturingLlm($"Answer. [{chunkId}]");
        var generation = new GenerationService(llm);
        var names = new Dictionary<string, string?> { [docId] = "maria-cv.pdf" };

        await generation.GenerateAsync("What does Maria do?", chunks, names);

        llm.LastSystemPrompt.Should().Contain("ONLY chunks that mention that same person");
        llm.LastSystemPrompt.Should().Contain("never the whole list");
        llm.LastSystemPrompt.Should().Contain("same language as the question");
        llm.LastUserPrompt.Should().Contain("maria-cv.pdf");
        llm.LastUserPrompt.Should().Contain("answer only from chunks about that person");
    }

    private static SearchResult MakeResult(string chunkId, float score, string text = "text")
    {
        return new SearchResult(Guid.Parse(chunkId), Guid.NewGuid().ToString(), text, 0, score);
    }

    private sealed class ScoringStore : IVectorStore
    {
        private readonly IReadOnlyList<SearchResult> _results;

        public ScoringStore(IReadOnlyList<SearchResult> results) => _results = results;

        public Task UpsertAsync(
            IEnumerable<VectorRecord> vectors,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] q,
            int limit,
            string? f = null,
            CancellationToken ct = default
        ) => Task.FromResult(_results);

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FixedEmbedder : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken ct = default
        ) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(_ => new float[] { 1f, 0f }).ToList()
            );
    }

    private sealed class FixedLlm : ILlmClient
    {
        private readonly string _response;

        public FixedLlm(string response) => _response = response;

        public Task<string> ChatAsync(string s, string u, CancellationToken ct = default) =>
            Task.FromResult(_response);

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class CapturingLlm : ILlmClient
    {
        private readonly string _response;

        public CapturingLlm(string response) => _response = response;

        public string LastSystemPrompt { get; private set; } = string.Empty;

        public string LastUserPrompt { get; private set; } = string.Empty;

        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default
        )
        {
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            return Task.FromResult(_response);
        }

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
