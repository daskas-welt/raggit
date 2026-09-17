using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Contract tests for <c>POST /api/query</c> per contracts/api.yaml.
/// Runs against the full Workstation.Api via <see cref="WebApplicationFactory{Program}"/>
/// with deterministic fakes so the tests do not require Ollama/LanceDB.
/// </summary>
public sealed class QueryContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        GetVectorStore().Clear();
    }

    [Fact]
    public async Task Post_QueryWithRelevantChunks_Returns200_AnswerAndCitations()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var vectorStore = GetVectorStore();
        vectorStore.Seed(
            new[]
            {
                new VectorRecord(
                    chunkId,
                    new float[384],
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = documentId.ToString(),
                        ["text"] = "Customers may return items within 30 days for a full refund.",
                        ["ordinal"] = 0,
                    }
                ),
            }
        );

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = $"Customers can return items within 30 days. [{chunkId}]";

        var response = await _client.PostAsJsonAsync(
            "/api/query",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);

        var citation = json.GetProperty("citations")[0];
        citation.GetProperty("documentId").GetString().Should().Be(documentId.ToString());
        citation.GetProperty("chunkId").GetString().Should().Be(chunkId.ToString());
        citation
            .GetProperty("text")
            .GetString()
            .Should()
            .Be("Customers may return items within 30 days for a full refund.");
        citation.GetProperty("ordinal").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Post_QueryWithNoRelevantChunks_Returns200_NoRelevantContent()
    {
        GetVectorStore().Clear();

        var response = await _client.PostAsJsonAsync(
            "/api/query",
            new { query = "xyz nonsense query" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Post_QueryAsAdmin_Returns200_AttributedToAdmin()
    {
        // Regression test: admins must be allowed to query (Roles = "Employee,Admin").
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var vectorStore = GetVectorStore();
        vectorStore.Seed(
            new[]
            {
                new VectorRecord(
                    chunkId,
                    new float[384],
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = documentId.ToString(),
                        ["text"] = "Customers may return items within 30 days for a full refund.",
                        ["ordinal"] = 0,
                    }
                ),
            }
        );

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = $"Customers can return items within 30 days. [{chunkId}]";

        using var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);

        var response = await adminClient.PostAsJsonAsync(
            "/api/query",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);

        // API-key callers carry no JWT sub, so attribution falls back to the
        // key identity ("admin"). Verify the audit row directly: the 005
        // history view intentionally excludes legacy admin/employee rows.
        var db = _factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM Queries ORDER BY CreatedAt DESC LIMIT 1;";
        var userId = (string?)await command.ExecuteScalarAsync();
        userId.Should().Be("admin");
    }

    private FakeVectorStore GetVectorStore() =>
        (FakeVectorStore)_factory.Services.GetRequiredService<IVectorStore>();

    private FakeLlmClient GetLlmClient() =>
        (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
}
