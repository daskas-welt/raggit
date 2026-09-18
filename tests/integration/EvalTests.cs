using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Eval harness for SC-003 (retrieval precision) and SC-004 (citation coverage /
/// zero hallucination). Seeds LanceDB with synthetic chunked documents across
/// known categories, then runs 50 Q/A pairs through <c>POST /api/queries</c>.
/// Uses deterministic fakes so no WAN/Ollama is required.
/// </summary>
public sealed class EvalTests : IClassFixture<IntegrationTestFactory>
{
    private const int VectorSize = 384;
    private const int TopK = 5;

    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    private readonly List<Category> _categories = new();
    private readonly Dictionary<Guid, int> _chunkCategoryIndex = new();

    public EvalTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
    }

    [Fact]
    public async Task Eval_FiftyQuestions_MeetsSc003AndSc004()
    {
        await SeedLibraryAsync();
        var questions = BuildQuestions();

        var relevantCount = 0;
        var relevantAsked = 0;
        var citationCoverage = 0;
        var hallucinationCount = 0;

        var llm = (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = true;
        llm.ThrowOnChat = false;

        foreach (var question in questions)
        {
            llm.ResponseText = $"Answer for {question.ExpectedCategory}.";

            var response = await _client.PostAsJsonAsync(
                "/api/queries",
                new { query = question.Query, topK = TopK },
                _jsonOptions
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            var answer = json.GetProperty("answer").GetString() ?? string.Empty;
            var citations = json.GetProperty("citations").EnumerateArray().ToList();
            var retrievedChunkIds = json.TryGetProperty("retrievedChunkIds", out var retrieved)
                ? retrieved.EnumerateArray().Select(x => Guid.Parse(x.GetString()!)).ToList()
                : new List<Guid>();

            if (question.ExpectedCategory >= 0)
            {
                relevantAsked++;
                var hasRelevant = retrievedChunkIds.Any(id =>
                    _chunkCategoryIndex.GetValueOrDefault(id, -1) == question.ExpectedCategory
                );
                if (hasRelevant)
                {
                    relevantCount++;
                }

                if (citations.Count > 0)
                {
                    citationCoverage++;
                }
            }
            else
            {
                if (!answer.Equals("no relevant content found", StringComparison.OrdinalIgnoreCase))
                {
                    hallucinationCount++;
                }
            }
        }

        var precision = relevantAsked == 0 ? 1.0 : relevantCount / (double)relevantAsked;
        precision.Should().BeGreaterOrEqualTo(0.80, "SC-003 requires >=80% top-5 relevance");

        citationCoverage
            .Should()
            .Be(relevantAsked, "SC-004 requires 100% citation coverage for sourced answers");
        hallucinationCount
            .Should()
            .Be(0, "SC-004 requires 0% hallucination for queries without relevant source");
    }

    private async Task SeedLibraryAsync()
    {
        var store = _factory.Services.GetRequiredService<IVectorStore>();
        var embedder = (FakeEmbedder)_factory.Services.GetRequiredService<IEmbedder>();

        var records = new List<VectorRecord>();
        var overrides = new Dictionary<string, float[]>();

        for (var i = 0; i < 10; i++)
        {
            var name = $"category-{i}";
            var docId = Guid.NewGuid();
            var vector = new float[VectorSize];
            vector[i] = 1.0f;

            var category = new Category
            {
                Index = i,
                Name = name,
                DocumentId = docId,
            };
            _categories.Add(category);
            overrides[name] = vector;

            for (var ordinal = 0; ordinal < 3; ordinal++)
            {
                var chunkId = Guid.NewGuid();
                category.ChunkIds.Add(chunkId);
                _chunkCategoryIndex[chunkId] = i;

                records.Add(
                    new VectorRecord(
                        chunkId,
                        vector,
                        new Dictionary<string, object?>
                        {
                            ["documentId"] = docId.ToString(),
                            ["text"] =
                                $"This chunk describes {name} policy details (ordinal {ordinal}).",
                            ["ordinal"] = ordinal,
                        }
                    )
                );
            }
        }

        // Queries containing this word intentionally miss every seeded vector.
        var noMatchVector = new float[VectorSize];
        noMatchVector[30] = 1.0f;
        overrides["unrelated"] = noMatchVector;

        // Distractor documents with vectors orthogonal to all categories.
        for (var d = 0; d < 5; d++)
        {
            var docId = Guid.NewGuid();
            var vector = new float[VectorSize];
            vector[10 + d] = 1.0f;

            for (var ordinal = 0; ordinal < 2; ordinal++)
            {
                var chunkId = Guid.NewGuid();
                _chunkCategoryIndex[chunkId] = -1;

                records.Add(
                    new VectorRecord(
                        chunkId,
                        vector,
                        new Dictionary<string, object?>
                        {
                            ["documentId"] = docId.ToString(),
                            ["text"] = $"Distractor {d} content (ordinal {ordinal}).",
                            ["ordinal"] = ordinal,
                        }
                    )
                );
            }
        }

        embedder.VectorOverrides = overrides;
        await store.UpsertAsync(records);
    }

    private List<EvalQuestion> BuildQuestions()
    {
        var questions = new List<EvalQuestion>();

        // 40 relevant queries (4 per category).
        foreach (var category in _categories)
        {
            for (var q = 0; q < 4; q++)
            {
                questions.Add(
                    new EvalQuestion
                    {
                        Query = $"What is the {category.Name} policy? variation {q}",
                        ExpectedCategory = category.Index,
                    }
                );
            }
        }

        // 10 no-relevant-content queries. The word "unrelated" is mapped in
        // FakeEmbedder to a vector dimension no seeded chunk uses.
        for (var q = 0; q < 10; q++)
        {
            questions.Add(
                new EvalQuestion
                {
                    Query = $"unrelated topic {Guid.NewGuid():N} {q}",
                    ExpectedCategory = -1,
                }
            );
        }

        return questions;
    }

    private sealed class Category
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid DocumentId { get; set; }
        public List<Guid> ChunkIds { get; } = new();
    }

    private sealed class EvalQuestion
    {
        public string Query { get; set; } = string.Empty;
        public int ExpectedCategory { get; set; }
    }
}
