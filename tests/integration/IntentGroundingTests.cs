using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
/// T034/T035 (US4): grounding holds across every intent — no relevant
/// content yields exactly <c>no relevant content found</c> with zero
/// citations in auto-resolved and forced modes, and an ambiguous query
/// falls back to granular with a grounded answer (never ungrounded text).
/// </summary>
public sealed class IntentGroundingTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(null)]
    [InlineData("auto")]
    [InlineData("broad")]
    [InlineData("specific")]
    public async Task NoRelevantContent_AcrossAllModes_ReturnsExactMessage_ZeroCitations(
        string? mode
    )
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        await UploadTextAsync(adminClient, "The refund policy allows returns within 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        // "unrelated" maps to a vector dimension no seeded chunk uses.
        var embedder = GetEmbedder(factory);
        var noMatch = new float[384];
        noMatch[30] = 1.0f;
        embedder.VectorOverrides = new Dictionary<string, float[]>
        {
            ["unrelated"] = noMatch,
        };

        object payload = mode is null
            ? new { query = "unrelated topic quasar" }
            : new { query = "unrelated topic quasar", mode };
        var response = await employeeClient.PostAsJsonAsync("/api/queries", payload, _jsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AmbiguousQuery_FallsBackToGranular_GroundedOrNoContent()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        await UploadTextAsync(adminClient, "The refund policy allows returns within 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "Refunds are accepted within 30 days.";

        // No broad trigger and no explicit override: granular fallback.
        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "Tell me about the thing" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var answer = json.GetProperty("answer").GetString()!;
        var citations = json.GetProperty("citations").EnumerateArray().ToList();

        // Either a cited (grounded) answer or the exact no-content message —
        // never ungrounded text.
        if (answer != "no relevant content found")
        {
            citations.Should().NotBeEmpty("a sourced answer must carry citations");
        }
        else
        {
            citations.Should().BeEmpty();
        }

        json.GetProperty("mode").GetString().Should().Be("granular");
    }

    private async Task UploadTextAsync(HttpClient adminClient, string text)
    {
        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes($"{text} {unique}")));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"grounding-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    private static HttpClient CreateEmployeeClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        return client;
    }

    private static HttpClient CreateAdminClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        return client;
    }

    private static FakeEmbedder GetEmbedder(IntegrationTestFactory factory) =>
        (FakeEmbedder)factory.Services.GetRequiredService<IEmbedder>();

    private static FakeLlmClient GetLlmClient(IntegrationTestFactory factory) =>
        (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
}
