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
/// Offline/LAN-only integration tests for <c>POST /api/queries</c>.
/// Uses the real Workstation.Api with a temp LanceDB vector store and
/// deterministic fakes for Ollama embed/chat so no WAN egress is required.
/// Each test creates its own factory to keep test data isolated.
/// </summary>
public sealed class QueryOfflineTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Query_LanOnly_NoWanEgress_ReturnsAnswerWithCitations_p95_Under7Seconds()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        await SeedRefundPolicyDocumentAsync(factory);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "Refunds are accepted within 30 days.";

        const int iterations = 10;
        var latencies = new List<long>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await employeeClient.PostAsJsonAsync(
                "/api/queries",
                new { query = "refund policy" },
                _jsonOptions
            );
            sw.Stop();

            response
                .StatusCode.Should()
                .Be(HttpStatusCode.OK, $"iteration {i} should succeed offline");
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
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "xyz irrelevant" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Query_LlmUnavailable_Returns503()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        await SeedRefundPolicyDocumentAsync(factory);

        var llm = GetLlmClient(factory);
        llm.Healthy = false;
        llm.ThrowOnChat = true;

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static HttpClient CreateEmployeeClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        return client;
    }

    private async Task SeedRefundPolicyDocumentAsync(IntegrationTestFactory factory)
    {
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var bytes = Encoding.UTF8.GetBytes(
            $"The refund policy allows returns within 30 days with a full refund. {unique}"
        );
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"refund-policy-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<Document>(_jsonOptions);
        staged.Should().NotBeNull();

        // Uploads are accepted for background indexing: wait until the worker
        // reports Ready, otherwise retrieval finds no chunks yet.
        var settled = await factory.WaitForSettledAsync(adminClient);
        var indexed = settled.Single(d => d.Id == staged!.Id);
        indexed
            .Status.Should()
            .Be(DocumentStatus.Ready, $"seeding failed: {indexed.FailureReason}");
    }

    private static FakeLlmClient GetLlmClient(IntegrationTestFactory factory) =>
        (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
}
