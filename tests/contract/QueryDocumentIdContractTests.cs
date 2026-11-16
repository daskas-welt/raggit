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

namespace RAGGit.Tests.Contract;

/// <summary>
/// 033: the answer response and the streamed terminal event carry the persisted
/// query id used by the document endpoint, without changing any existing field.
/// </summary>
public sealed class QueryDocumentIdContractTests : IClassFixture<TestApiFactory>
{
    private const string ChunkText = "Customers may return items within 30 days for a full refund.";

    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryDocumentIdContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ChatException = null;
        GetVectorStore().Clear();
    }

    [Fact]
    public async Task ClassicAnswer_CarriesThePersistedQueryId()
    {
        SeedOneChunk();

        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        json.GetProperty("answer").GetString().Should().Contain("30 days");
        json.GetProperty("citations").GetArrayLength().Should().Be(1);
        var queryId = json.GetProperty("queryId").GetString();
        Guid.TryParse(queryId, out var parsed).Should().BeTrue();
        parsed.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StreamedAnswer_CarriesThePersistedQueryId()
    {
        SeedOneChunk();

        var response = await _client.PostAsJsonAsync(
            "/api/queries/stream",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await ReadEventsAsync(response);
        var answer = events.Last();

        answer.IsAnswer.Should().BeTrue();
        answer.QueryId.Should().NotBeNull();
        answer.QueryId.Should().NotBe(Guid.Empty);
    }

    private (Guid DocumentId, Guid ChunkId) SeedOneChunk()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        GetVectorStore()
            .Seed(
                new[]
                {
                    new VectorRecord(
                        chunkId,
                        new float[384],
                        new Dictionary<string, object?>
                        {
                            ["documentId"] = documentId.ToString(),
                            ["text"] = ChunkText,
                            ["ordinal"] = 0,
                        }
                    ),
                }
            );

        GetLlmClient().ResponseText = $"Customers can return items within 30 days. [{chunkId}]";
        return (documentId, chunkId);
    }

    private async Task<List<QueryStreamEvent>> ReadEventsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var events = new List<QueryStreamEvent>();

        foreach (
            var line in body.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var streamEvent = JsonSerializer.Deserialize<QueryStreamEvent>(line, _jsonOptions);
            streamEvent
                .Should()
                .NotBeNull($"every NDJSON line must be a well-formed event: {line}");
            events.Add(streamEvent!);
        }

        events.Should().NotBeEmpty();
        return events;
    }

    private FakeVectorStore GetVectorStore() =>
        (FakeVectorStore)_factory.Services.GetRequiredService<IVectorStore>();

    private FakeLlmClient GetLlmClient() =>
        (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
}
