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
/// The query-mode override is gone: responses carry no <c>mode</c>, and a
/// legacy <c>mode</c> field is ignored like any other unknown JSON property.
/// </summary>
public sealed class QueryModeRemovalContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryModeRemovalContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        GetVectorStore().Clear();
    }

    [Fact]
    public async Task Post_Response_HasNoMode()
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
        json.TryGetProperty("mode", out _).Should().BeFalse();
        json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_LegacyModeField_IsIgnored()
    {
        SeedRefundChunk(out _);

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = "Customers can return items within 30 days.";

        var response = await _client.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy", mode = "banana" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
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
