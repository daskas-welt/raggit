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
/// 032 T013/T026: contract tests for <c>POST /api/queries/stream</c> per
/// contracts/api.yaml 1.6.0 — NDJSON framing, event ordering, the classic
/// status codes before the first byte, in-band failures, redaction, and parity
/// with the non-streaming endpoint. Runs against the full Workstation.Api with
/// deterministic fakes, so no Ollama/LanceDB is required.
/// </summary>
public sealed class QueryStreamContractTests : IClassFixture<TestApiFactory>
{
    private const string ChunkText = "Customers may return items within 30 days for a full refund.";

    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryStreamContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ChatException = null;
        llm.ResponseText = "fake answer";
        GetVectorStore().Clear();
    }

    [Fact]
    public async Task Post_Stream_ReturnsNdjson_WithStagesThenTheAnswer()
    {
        var (documentId, chunkId) = SeedOneChunk();

        var response = await PostStreamAsync("refund policy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-ndjson");

        var events = await ReadEventsAsync(response);

        events
            .Where(streamEvent => streamEvent.IsProgress)
            .Select(streamEvent => streamEvent.Stage)
            .Should()
            .Equal(
                QueryStage.Understanding,
                QueryStage.Searching,
                QueryStage.Sources,
                QueryStage.Composing
            );

        var answer = events.Last();
        answer.IsAnswer.Should().BeTrue();
        answer.Answer.Should().Contain("30 days");
        answer.Citations.Should().ContainSingle();
        answer.Citations![0].ChunkId.Should().Be(chunkId);
        answer.Citations![0].DocumentId.Should().Be(documentId);
    }

    [Fact]
    public async Task Post_Stream_AnswerEvent_MatchesTheNonStreamingEndpoint()
    {
        SeedOneChunk();
        GetLlmClient().ResponseText = "Customers can return items within 30 days. [see below]";

        var classic = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy" },
            _jsonOptions
        );
        classic.StatusCode.Should().Be(HttpStatusCode.OK);
        var classicJson = await classic.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        var streamed = (await ReadEventsAsync(await PostStreamAsync("refund policy"))).Last();

        streamed.Answer.Should().Be(classicJson.GetProperty("answer").GetString());
        streamed
            .Citations!.Count.Should()
            .Be(classicJson.GetProperty("citations").GetArrayLength());
        streamed
            .RetrievedChunkIds!.Count.Should()
            .Be(classicJson.GetProperty("retrievedChunkIds").GetArrayLength());
    }

    [Fact]
    public async Task Post_Stream_WithoutRelevantChunks_NeverClaimsSources()
    {
        GetVectorStore().Clear();

        var events = await ReadEventsAsync(await PostStreamAsync("nothing matches this"));

        events
            .Where(streamEvent => streamEvent.IsProgress)
            .Select(streamEvent => streamEvent.Stage)
            .Should()
            .NotContain(QueryStage.Sources);

        var answer = events.Last();
        answer.IsAnswer.Should().BeTrue();
        answer.Answer.Should().Be("no relevant content found");
        answer.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_Stream_SourceFields_AppearOnlyOnTheSourcesStage()
    {
        SeedOneChunk();

        var events = await ReadEventsAsync(await PostStreamAsync("refund policy"));

        var sources = events.Single(streamEvent => streamEvent.Stage == QueryStage.Sources);
        sources.SourceCount.Should().Be(1);
        sources.DocumentNames.Should().NotBeNull();

        events
            .Where(streamEvent => streamEvent.IsProgress && streamEvent.Stage != QueryStage.Sources)
            .Should()
            .OnlyContain(streamEvent =>
                streamEvent.SourceCount == null && streamEvent.DocumentNames == null
            );
    }

    [Fact]
    public async Task Post_Stream_ProgressEvents_LeakNoChunkTextPromptOrIdentifiers()
    {
        var (documentId, chunkId) = SeedOneChunk();

        var events = await ReadEventsAsync(await PostStreamAsync("refund policy"));

        var progress = events.Where(streamEvent => streamEvent.IsProgress).ToList();
        progress.Should().NotBeEmpty();

        foreach (var update in progress)
        {
            var text = update.Text ?? string.Empty;
            text.Should().NotContain(ChunkText);
            text.Should().NotContain("You are a helpful assistant");
            text.Should().NotContain("Context:");
            text.Should().NotContain(documentId.ToString());
            text.Should().NotContain(chunkId.ToString());
        }

        // The answer event may carry the citation text: that is the answer's own
        // Sources view, which exists today (FR-007 allows exactly that much).
        events.Last().Citations.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_Stream_Unauthenticated_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/queries/stream",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Stream_InvalidQuery_IsRejectedExactlyLikeTheNonStreamingEndpoint()
    {
        // A whitespace-only query is rejected by model validation before either
        // action runs, so the two transports must produce the same rejection.
        var streamed = await PostStreamAsync("   ");
        var classic = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "   " },
            _jsonOptions
        );

        streamed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        classic.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        streamed
            .Content.Headers.ContentType!.MediaType.Should()
            .Be(classic.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Post_Stream_UnhealthyWorkstation_Returns503Json_NotAStream()
    {
        GetLlmClient().Healthy = false;

        var response = await PostStreamAsync("refund policy");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Contain("AI workstation unavailable");
    }

    [Fact]
    public async Task Post_Stream_FailureAfterTheFirstEvent_EmitsATerminalErrorEvent()
    {
        SeedOneChunk();
        GetLlmClient().ChatException = new HttpRequestException("simulated outage");

        var events = await ReadEventsAsync(await PostStreamAsync("refund policy"));

        events.Should().Contain(streamEvent => streamEvent.IsProgress);
        events.Should().NotContain(streamEvent => streamEvent.IsAnswer);

        var error = events.Last();
        error.IsError.Should().BeTrue();
        error.Error.Should().Be("model unavailable offline");
        error.Status.Should().Be(503);

        // The classic endpoint words the same failure the same way (FR-012).
        var classic = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy" },
            _jsonOptions
        );
        classic.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await classic.Content.ReadAsStringAsync()).Should().Contain("model unavailable offline");
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

    private Task<HttpResponseMessage> PostStreamAsync(string query) =>
        _client.PostAsJsonAsync("/api/queries/stream", new { query }, _jsonOptions);

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
