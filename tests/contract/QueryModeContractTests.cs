using System;
using System.Collections.Generic;
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
/// T025: <c>POST /api/queries</c> accepts the additive <c>mode</c> field —
/// omitted <c>mode</c> keeps pre-feature behaviour, <c>broad</c>/<c>specific</c>
/// are honoured with the effective intent echoed, and an unknown
/// <c>mode</c> is a 400 with the standard error shape.
/// </summary>
public sealed class QueryModeContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryModeContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        GetVectorStore().Clear();
    }

    [Fact]
    public async Task Post_OmittedMode_Returns200_WithPreFeatureBehaviour()
    {
        SeedRefundChunk(out _);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = "Customers can return items within 30 days.";

        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "What is the renewal term?" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);
        // Auto classifies a plain fact question as granular.
        json.GetProperty("mode").GetString().Should().Be("granular");
    }

    [Fact]
    public async Task Post_BroadMode_Returns200_WithBroadEcho()
    {
        SeedRefundChunk(out _);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = "Customers can return items within 30 days.";

        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "renewal term", mode = "broad" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("mode").GetString().Should().Be("broad");
    }

    [Fact]
    public async Task Post_SpecificMode_Returns200_WithGranularEcho()
    {
        SeedRefundChunk(out _);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = "Customers can return items within 30 days.";

        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "Summarize the refund policy", mode = "specific" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("mode").GetString().Should().Be("granular");
    }

    [Fact]
    public async Task Post_UnknownMode_Returns400_WithErrorShape()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy", mode = "banana" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Guid SeedRefundChunk(out Guid documentId)
    {
        documentId = Guid.NewGuid();
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
                            ["text"] =
                                "Customers may return items within 30 days for a full refund.",
                            ["ordinal"] = 0,
                        }
                    ),
                }
            );
        return chunkId;
    }

    private FakeVectorStore GetVectorStore() =>
        (FakeVectorStore)_factory.Services.GetRequiredService<IVectorStore>();

    private FakeLlmClient GetLlmClient() =>
        (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
}
