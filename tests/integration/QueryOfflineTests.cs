using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Offline/LAN-only integration tests for <c>POST /api/query</c>.
/// Uses the real Workstation.Api with a temp LanceDB vector store and
/// deterministic fakes for Ollama embed/chat so no WAN egress is required.
/// </summary>
public sealed class QueryOfflineTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() }
    };

    public QueryOfflineTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Query_LanOnly_NoWanEgress_ReturnsAnswerWithCitations_p95_Under7Seconds()
    {
        var employeeClient = CreateEmployeeClient();
        await SeedRefundPolicyDocumentAsync();

        var llm = GetLlmClient();
        llm.Healthy = true;
        llm.ResponseText = "Refunds are accepted within 30 days.";

        const int iterations = 10;
        var latencies = new List<long>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await employeeClient.PostAsJsonAsync("/api/query", new { query = "refund policy" }, _jsonOptions);
            sw.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"iteration {i} should succeed offline");
            latencies.Add(sw.ElapsedMilliseconds);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
            json.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);
        }

        var ordered = latencies.OrderBy(x => x).ToList();
        var p95Index = (int)Math.Ceiling(ordered.Count * 0.95) - 1;
        var p95 = ordered[Math.Max(0, p95Index)];
        p95.Should().BeLessThan(7000, "SC-002 requires p95 query latency <7s offline");
    }

    [Fact]
    public async Task Query_NoRelevantContent_ReturnsNoRelevantContent()
    {
        var employeeClient = CreateEmployeeClient();
        await SeedRefundPolicyDocumentAsync();

        var response = await employeeClient.PostAsJsonAsync("/api/query", new { query = "xyz irrelevant" }, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Query_LlmUnavailable_Returns503()
    {
        var employeeClient = CreateEmployeeClient();
        await SeedRefundPolicyDocumentAsync();

        var llm = GetLlmClient();
        llm.Healthy = false;
        llm.ThrowOnChat = true;

        var response = await employeeClient.PostAsJsonAsync("/api/query", new { query = "refund policy" }, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private HttpClient CreateEmployeeClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        return client;
    }

    private async Task SeedRefundPolicyDocumentAsync()
    {
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);

        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var bytes = Encoding.UTF8.GetBytes($"The refund policy allows returns within 30 days with a full refund. {unique}");
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"refund-policy-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    private FakeLlmClient GetLlmClient()
        => (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
}
