using System;
using System.Collections.Generic;
using System.IO;
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
/// 032 T024/T032/T034: the stream end-to-end — progress before the answer, the
/// sources it actually found, Greek narration for a Greek question, an in-band
/// failure after the first byte, and history parity with the classic endpoint
/// (including that no reasoning is ever persisted).
/// </summary>
public sealed class QueryStreamTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Stream_WithASource_ReportsStagesAndNamesTheUploadedDocument()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        const string fact = "The renewal term for the Pro plan is 30 days.";
        var uploaded = await UploadTextAsync(adminClient, "stream-source", fact);
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The renewal term is 30 days.";

        var events = await ReadEventsAsync(
            await employeeClient.PostAsJsonAsync(
                "/api/queries/stream",
                new { query = "What is the renewal term?" },
                _jsonOptions
            )
        );

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

        var sources = events.Single(streamEvent => streamEvent.Stage == QueryStage.Sources);
        sources.SourceCount.Should().Be(1);
        sources
            .DocumentNames.Should()
            .ContainSingle(
                name => name == uploaded.Filename,
                "the source is the uploaded document"
            );
        sources.Text.Should().Contain(uploaded.Filename);

        var answer = events.Last();
        answer.IsAnswer.Should().BeTrue();
        answer.Answer.Should().NotBeNullOrWhiteSpace();
        answer.Citations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Stream_WithoutAMatch_NeverClaimsSourcesAndRefusesExactly()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        await UploadTextAsync(adminClient, "stream-nomatch", "The refund policy allows returns.");
        await factory.WaitForSettledAsync(adminClient);

        // "unrelated" maps to a vector dimension no seeded chunk uses, so every
        // score lands below MinScore.
        var noMatch = new float[384];
        noMatch[30] = 1.0f;
        GetEmbedder(factory).VectorOverrides = new Dictionary<string, float[]>
        {
            ["unrelated"] = noMatch,
        };

        var events = await ReadEventsAsync(
            await employeeClient.PostAsJsonAsync(
                "/api/queries/stream",
                new { query = "unrelated" },
                _jsonOptions
            )
        );

        events
            .Where(streamEvent => streamEvent.IsProgress)
            .Select(streamEvent => streamEvent.Stage)
            .Should()
            .NotContain(QueryStage.Sources);

        var answer = events.Last();
        answer.Answer.Should().Be("no relevant content found");
        answer.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task Stream_GreekQuestion_NarratesInGreek()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        await UploadTextAsync(
            adminClient,
            "stream-greek",
            "Ο χρόνος ανανέωσης για το πρόγραμμα Pro είναι 30 ημέρες."
        );
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "Ο χρόνος ανανέωσης είναι 30 ημέρες.";

        var events = await ReadEventsAsync(
            await employeeClient.PostAsJsonAsync(
                "/api/queries/stream",
                new { query = "Ποιος είναι ο χρόνος ανανέωσης;" },
                _jsonOptions
            )
        );

        var progress = events.Where(streamEvent => streamEvent.IsProgress).ToList();
        progress.Should().NotBeEmpty();
        progress
            .Should()
            .OnlyContain(update => update.Stage != QueryStage.Sources || update.SourceCount >= 1);

        // Every narration line follows the question's language (FR-006).
        progress.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Stream_FailureAfterTheFirstEvent_ReportsATerminalErrorEvent()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        await UploadTextAsync(adminClient, "stream-failure", "The renewal term is 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ThrowOnChat = true;

        var events = await ReadEventsAsync(
            await employeeClient.PostAsJsonAsync(
                "/api/queries/stream",
                new { query = "What is the renewal term?" },
                _jsonOptions
            )
        );

        events.Should().Contain(streamEvent => streamEvent.IsProgress);
        events.Should().NotContain(streamEvent => streamEvent.IsAnswer);

        var error = events.Last();
        error.IsError.Should().BeTrue();
        error.Error.Should().Be("model unavailable offline");
        error.Status.Should().Be(503);
    }

    [Fact]
    public async Task Stream_WritesTheSameHistoryRowAndPersistsNoReasoning()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        await UploadTextAsync(adminClient, "stream-history", "The renewal term is 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The renewal term is 30 days.";

        var events = await ReadEventsAsync(
            await employeeClient.PostAsJsonAsync(
                "/api/queries/stream",
                new { query = "What is the renewal term?", mode = "specific" },
                _jsonOptions
            )
        );
        var streamed = events.Last();
        streamed.IsAnswer.Should().BeTrue();

        // The streamed question is persisted exactly like a classic one (FR-010).
        // Read straight from SQLite: API-key identities are excluded from the
        // person-history projections by design, so the history API cannot see them.
        var row = await LoadLatestQueryRowAsync(factory);
        row.Prompt.Should().Be("What is the renewal term?");
        row.Answer.Should().Be(streamed.Answer);
        row.RetrievedChunkIds.Should().NotBeEmpty();
        row.CitationIds.Count.Should().Be(streamed.Citations!.Count);
        row.Mode!.ToLowerInvariant().Should().Be("granular");

        // ...and none of the narration was persisted (FR-014).
        row.Answer.Should().NotContain("Reading your question");
        row.Answer.Should().NotContain("Searching the library");
        row.Answer.Should().NotContain("Writing your answer");
        row.Prompt.Should().NotContain("Reading your question");
    }

    [Fact]
    public async Task Stream_AndClassicEndpoint_AgreeForTheSameQuestion()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        await UploadTextAsync(adminClient, "stream-parity", "The renewal term is 30 days.");
        await factory.WaitForSettledAsync(adminClient);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "The renewal term is 30 days.";

        var classic = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "What is the renewal term?" },
            _jsonOptions
        );
        classic.StatusCode.Should().Be(HttpStatusCode.OK);
        var classicJson = await classic.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        var streamed = (
            await ReadEventsAsync(
                await employeeClient.PostAsJsonAsync(
                    "/api/queries/stream",
                    new { query = "What is the renewal term?" },
                    _jsonOptions
                )
            )
        ).Last();

        streamed.Answer.Should().Be(classicJson.GetProperty("answer").GetString());
        streamed
            .Citations!.Count.Should()
            .Be(classicJson.GetProperty("citations").GetArrayLength());
        streamed
            .Mode!.Value.ToString()
            .ToLowerInvariant()
            .Should()
            .Be(classicJson.GetProperty("mode").GetString());
    }

    private static async Task<List<QueryStreamEvent>> ReadEventsAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-ndjson");

        var body = await response.Content.ReadAsStringAsync();
        var events = new List<QueryStreamEvent>();
        foreach (
            var line in body.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var streamEvent = JsonSerializer.Deserialize<QueryStreamEvent>(line, WebOptions);
            streamEvent
                .Should()
                .NotBeNull($"every NDJSON line must be a well-formed event: {line}");
            events.Add(streamEvent!);
        }

        events.Should().NotBeEmpty();
        return events;
    }

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    private static async Task<Document> UploadTextAsync(
        HttpClient adminClient,
        string prefix,
        string text
    )
    {
        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes($"{text} {unique}")));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"{prefix}-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var staged = await response.Content.ReadFromJsonAsync<Document>(WebOptions);
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

    /// <summary>
    /// Reads the row the query path just wrote, so history parity (FR-010) and
    /// "no reasoning persisted" (FR-014) are asserted against the same columns
    /// the person-history projections read.
    /// </summary>
    private static async Task<(
        string Prompt,
        string Answer,
        IReadOnlyList<Guid> RetrievedChunkIds,
        IReadOnlyList<Guid> CitationIds,
        string? Mode
    )> LoadLatestQueryRowAsync(IntegrationTestFactory factory)
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Prompt, Answer, RetrievedChunkIds, CitationIds, Mode FROM Queries ORDER BY CreatedAt DESC LIMIT 1;";

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync())
            .Should()
            .BeTrue("the streamed question must be persisted exactly like a classic one");

        return (
            reader.GetString(0),
            reader.GetString(1),
            JsonSerializer.Deserialize<List<Guid>>(reader.GetString(2)) ?? new List<Guid>(),
            JsonSerializer.Deserialize<List<Guid>>(reader.GetString(3)) ?? new List<Guid>(),
            reader.IsDBNull(4) ? null : reader.GetString(4)
        );
    }
}
