using System;
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
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T015 (US1 granular): a fact question returns the exact fact with a
/// tightly-scoped child-chunk citation; an absent fact returns exactly
/// <c>no relevant content found</c> with empty citations.
/// T018 extends this file with the broad-synthesis half.
/// </summary>
public sealed class IntentRetrievalTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task GranularFactQuery_ReturnsExactFact_WithChildChunkCitation()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        const string fact = "The renewal term for the Pro plan is 30 days.";
        var staged = await UploadTextAsync(
            adminClient,
            $"{fact} This paragraph carries surrounding context so the fact sits mid-text, exactly as a real policy document would read in production."
        );
        var settled = await factory.WaitForSettledAsync(adminClient);
        settled.Single(d => d.Id == staged.Id).Status.Should().Be(DocumentStatus.Ready);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The renewal term is 30 days.";

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "What is the renewal term?", mode = "auto" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().Be("The renewal term is 30 days.");

        var citations = json.GetProperty("citations").EnumerateArray().ToList();
        citations.Should().ContainSingle();
        citations[0].GetProperty("text").GetString().Should().Contain("30 days");

        // The cited chunk is a tightly-scoped child, not a broad excerpt.
        var citedId = Guid.Parse(citations[0].GetProperty("chunkId").GetString()!);
        var chunk = await LoadChunkAsync(factory, citedId);
        chunk.Should().NotBeNull();
        chunk!.Level.Should().Be(ChunkLevel.Child);
    }

    [Fact]
    public async Task AbsentFact_ReturnsExactlyNoRelevantContent_WithEmptyCitations()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        await UploadTextAsync(adminClient, "The refund policy allows returns within 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        // "unrelated" maps to a vector dimension no seeded chunk uses, so
        // every score lands below MinScore.
        var embedder = GetEmbedder(factory);
        var noMatch = new float[384];
        noMatch[30] = 1.0f;
        embedder.VectorOverrides = new Dictionary<string, float[]> { ["unrelated"] = noMatch };

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "unrelated topic quasar banana" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task BroadSynthesisQuery_ReturnsParentContextCoveringAdjacentChildren()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        // ~1100 tokens: three child chunks, so the steps span adjacent units.
        var sections = Enumerable
            .Range(1, 8)
            .Select(step =>
                $"Step {step}: complete the onboarding task number {step} with care. "
                + string.Join(" ", Enumerable.Repeat($"onboarding-detail-{step}", 22))
            );
        var staged = await UploadTextAsync(adminClient, string.Join(" ", sections));
        var settled = await factory.WaitForSettledAsync(adminClient);
        settled.Single(d => d.Id == staged.Id).Status.Should().Be(DocumentStatus.Ready);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The onboarding process has eight steps, from step 1 to step 8.";

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "Summarize the onboarding process", mode = "auto" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("answer")
            .GetString()
            .Should()
            .Be("The onboarding process has eight steps, from step 1 to step 8.");

        var citations = json.GetProperty("citations").EnumerateArray().ToList();
        citations.Should().ContainSingle();

        // The cited passage is a larger parent unit spanning the child
        // boundary: it covers both the first and the last step.
        var citationText = citations[0].GetProperty("text").GetString()!;
        citationText.Should().Contain("Step 1:");
        citationText.Should().Contain("Step 8:");

        var citedId = Guid.Parse(citations[0].GetProperty("chunkId").GetString()!);
        var chunk = await LoadChunkAsync(factory, citedId);
        chunk.Should().NotBeNull();
        chunk!.Level.Should().Be(ChunkLevel.Parent);
    }

    [Fact]
    public async Task BroadAnswer_PersistsParentCitationIds_AndEchoesBroad()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = CreateEmployeeClient(factory);
        var adminClient = CreateAdminClient(factory);

        // Two child chunks: "alpha" lands in the first, "omega" in the last.
        var text = string.Join(
            " ",
            Enumerable.Range(1, 600).Select(i => $"filler-{i}")
        );
        var staged = await UploadTextAsync(adminClient, $"alpha marker {text} omega marker");
        var settled = await factory.WaitForSettledAsync(adminClient);
        settled.Single(d => d.Id == staged.Id).Status.Should().Be(DocumentStatus.Ready);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The document spans from alpha to omega.";

        var response = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "Summarize the document", mode = "broad" },
            _jsonOptions
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        json.GetProperty("mode").GetString().Should().Be("broad");

        var citations = json.GetProperty("citations").EnumerateArray().ToList();
        citations.Should().ContainSingle();
        var citedId = Guid.Parse(citations[0].GetProperty("chunkId").GetString()!);
        citations[0].GetProperty("text").GetString().Should().Contain("alpha marker");
        citations[0].GetProperty("text").GetString().Should().Contain("omega marker");

        // Mapping + persistence carry the parent id (not a child id).
        // The audit row stores the PascalCase enum name per data-model.md;
        // the API wire value stays lowercase per contracts/api.yaml.
        var (retrievedIds, citationIds, mode) = await LoadLatestQueryRowAsync(factory);
        retrievedIds.Should().Contain(citedId);
        citationIds.Should().Contain(citedId);
        mode.Should().Be("Broad");

        var chunk = await LoadChunkAsync(factory, citedId);
        chunk.Should().NotBeNull();
        chunk!.Level.Should().Be(ChunkLevel.Parent);
    }

    private async Task<Document> UploadTextAsync(HttpClient adminClient, string text)
    {
        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes($"{text} {unique}")));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"intent-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<Document>(_jsonOptions);
        staged.Should().NotBeNull();
        return staged!;
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

    private static FakeLlmClient GetLlmClient(IntegrationTestFactory factory) =>
        (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();

    private static FakeEmbedder GetEmbedder(IntegrationTestFactory factory) =>
        (FakeEmbedder)factory.Services.GetRequiredService<IEmbedder>();

    private static async Task<Chunk?> LoadChunkAsync(IntegrationTestFactory factory, Guid chunkId)
    {
        var documents = factory.Services.GetRequiredService<IDocumentRepository>();
        var chunks = await documents.GetChunksByIdsAsync(new[] { chunkId });
        return chunks.SingleOrDefault();
    }

    private static async Task<
        (IReadOnlyList<Guid> RetrievedIds, IReadOnlyList<Guid> CitationIds, string? Mode)
    > LoadLatestQueryRowAsync(IntegrationTestFactory factory)
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT RetrievedChunkIds, CitationIds, Mode FROM Queries ORDER BY CreatedAt DESC LIMIT 1;";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (
            JsonSerializer.Deserialize<List<Guid>>(reader.GetString(0)) ?? new List<Guid>(),
            JsonSerializer.Deserialize<List<Guid>>(reader.GetString(1)) ?? new List<Guid>(),
            reader.IsDBNull(2) ? null : reader.GetString(2)
        );
    }
}
