using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T005/T006: Contract tests for GET /api/queries/history per
/// specs/005-per-person-history/contracts/api.yaml (1.4.0).
/// </summary>
public sealed class HistoryContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HistoryContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task History_DefaultParams_ReturnsOwnOnly_MostRecentFirst()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"hist-default-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var otherSub = Guid.NewGuid().ToString();
        var baseTime = DateTime.UtcNow;
        await SeedQueriesAsync(
            sub,
            Enumerable
                .Range(0, 3)
                .Select(i => (baseTime.AddMinutes(-i), $"own prompt {i}", $"own answer {i}"))
        );
        await SeedQueriesAsync(otherSub, new[] { (baseTime, "other prompt", "other answer") });

        var client = CreateBearerClient(token);
        var response = await client.GetAsync("/api/queries/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ParsePageAsync(response);
        page.Total.Should().Be(3);
        page.Limit.Should().Be(20);
        page.Offset.Should().Be(0);
        page.Items.Should().HaveCount(3);
        page.Items.Select(i => i.PromptPreview)
            .Should()
            .ContainInOrder("own prompt 0", "own prompt 1", "own prompt 2");
        page.Items.Should().OnlyContain(i => !i.PromptPreview.Contains("other prompt"));
    }

    [Fact]
    public async Task History_Pages_AreDisjointAndStable()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"hist-pages-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var baseTime = DateTime.UtcNow;
        await SeedQueriesAsync(
            sub,
            Enumerable
                .Range(0, 5)
                .Select(i => (baseTime.AddMinutes(-i), $"stable prompt {i}", $"stable answer {i}"))
        );

        var client = CreateBearerClient(token);
        var first = await ParsePageAsync(
            await client.GetAsync("/api/queries/history?limit=2&offset=0")
        );
        var second = await ParsePageAsync(
            await client.GetAsync("/api/queries/history?limit=2&offset=2")
        );
        var all = await ParsePageAsync(
            await client.GetAsync("/api/queries/history?limit=4&offset=0")
        );

        first.Items.Should().HaveCount(2);
        second.Items.Should().HaveCount(2);
        first.Items.Select(i => i.Id).Should().NotIntersectWith(second.Items.Select(i => i.Id));
        first
            .Items.Select(i => i.Id)
            .Concat(second.Items.Select(i => i.Id))
            .Should()
            .BeEquivalentTo(all.Items.Select(i => i.Id), options => options.WithStrictOrdering());
        all.Total.Should().Be(5);
    }

    [Theory]
    [InlineData("limit=0", 20)]
    [InlineData("limit=500", 100)]
    [InlineData("limit=-3", 20)]
    public async Task History_LimitOutOfRange_IsClamped(string query, int expectedLimit)
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"hist-clamp-{Guid.NewGuid():N}",
            UserRole.Employee
        );

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(await client.GetAsync($"/api/queries/history?{query}"));

        page.Limit.Should().Be(expectedLimit);
    }

    [Fact]
    public async Task History_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/queries/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task History_NewUser_ReturnsEmptyPage()
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"hist-empty-{Guid.NewGuid():N}",
            UserRole.Employee
        );

        var client = CreateBearerClient(token);
        var response = await client.GetAsync("/api/queries/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ParsePageAsync(response);
        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task History_LongPromptAndAnswer_TruncatedTo121()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"hist-trunc-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var longPrompt = new string('p', 200);
        var longAnswer = new string('a', 200);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
            await db.InsertQueryAsync(
                new Query
                {
                    Id = Guid.NewGuid(),
                    UserId = sub,
                    Prompt = longPrompt,
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = longAnswer,
                    CitationIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
                    LatencyMs = 9,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(await client.GetAsync("/api/queries/history"));

        page.Total.Should().Be(1);
        var item = page.Items.Should().ContainSingle().Subject;
        item.PromptPreview.Should().HaveLength(121).And.EndWith("…");
        item.AnswerPreview.Should().HaveLength(121).And.EndWith("…");
        item.CitationCount.Should().Be(2);
    }

    [Fact]
    public async Task History_OffsetBeyondTotal_ReturnsEmpty_WithTotal()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"hist-offset-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        await SeedQueriesAsync(sub, new[] { (DateTime.UtcNow, "solo prompt", "solo answer") });

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(
            await client.GetAsync("/api/queries/history?limit=20&offset=99")
        );

        page.Total.Should().Be(1);
        page.Items.Should().BeEmpty();
        page.Offset.Should().Be(99);
    }

    // T013 [US2] Detail contract per contracts/api.yaml GET /api/queries/{id} (FR-004/FR-007).

    [Fact]
    public async Task Detail_OwnQuery_Returns200_WithFullAnswerAndOrderedCitations()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"hist-detail-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var queryId = Guid.NewGuid();
        var chunkA = Guid.NewGuid();
        var chunkB = Guid.NewGuid();
        var docId = Guid.NewGuid();
        await SeedChunksAsync(docId, new[] { (chunkB, 1, "second chunk"), (chunkA, 0, "first chunk") });
        await SeedQueryAsync(
            queryId,
            sub,
            "detail prompt",
            "detail full answer",
            new[] { chunkA, chunkB }
        );

        var client = CreateBearerClient(token);
        var response = await client.GetAsync($"/api/queries/{queryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ParseDetailAsync(response);
        detail.Id.Should().Be(queryId.ToString());
        detail.Prompt.Should().Be("detail prompt");
        detail.Answer.Should().Be("detail full answer");
        detail.Citations.Should().HaveCount(2);
        detail.Citations.Select(c => c.Ordinal).Should().ContainInOrder(0, 1);
        detail.Citations.Select(c => c.Text).Should().ContainInOrder("first chunk", "second chunk");
        detail.Citations.Should().OnlyContain(c => c.DocumentId == docId.ToString());
    }

    [Fact]
    public async Task Detail_NonOwner_Returns404()
    {
        var (ownerSub, _) = await ProvisionAndLoginAsync(
            $"hist-owner-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var (_, otherToken) = await ProvisionAndLoginAsync(
            $"hist-other-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var queryId = Guid.NewGuid();
        await SeedQueryAsync(queryId, ownerSub, "owner prompt", "owner answer", Array.Empty<Guid>());

        var client = CreateBearerClient(otherToken);
        var response = await client.GetAsync($"/api/queries/{queryId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_NotFound_Returns404()
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"hist-missing-{Guid.NewGuid():N}",
            UserRole.Employee
        );

        var client = CreateBearerClient(token);
        var response = await client.GetAsync($"/api/queries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/queries/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Detail_LegacyRow_Returns404_ForPersonCaller()
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"hist-legacy-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var legacyId = Guid.NewGuid();
        await SeedQueryAsync(legacyId, "admin", "legacy prompt", "legacy answer", Array.Empty<Guid>());

        var client = CreateBearerClient(token);
        var response = await client.GetAsync($"/api/queries/{legacyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient CreateBearerClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(string Sub, string Token)> ProvisionAndLoginAsync(
        string username,
        UserRole role
    )
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<UserStore>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword("history-pass-1"),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
        await store.CreateAsync(user);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "history-pass-1" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user.Id.ToString(), doc.RootElement.GetProperty("access_token").GetString()!);
    }

    private async Task SeedQueriesAsync(
        string sub,
        IEnumerable<(DateTime CreatedAt, string Prompt, string Answer)> rows
    )
    {
        foreach (var row in rows)
        {
            await SeedQueryAsync(Guid.NewGuid(), sub, row.Prompt, row.Answer, Array.Empty<Guid>(), row.CreatedAt);
        }
    }

    private async Task SeedQueryAsync(
        Guid id,
        string sub,
        string prompt,
        string answer,
        IReadOnlyList<Guid> citationIds,
        DateTime? createdAt = null
    )
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await db.InsertQueryAsync(
            new Query
            {
                Id = id,
                UserId = sub,
                Prompt = prompt,
                RetrievedChunkIds = Array.Empty<Guid>(),
                Answer = answer,
                CitationIds = citationIds,
                LatencyMs = 7,
                CreatedAt = createdAt ?? DateTime.UtcNow,
            }
        );
    }

    private async Task SeedChunksAsync(Guid documentId, IEnumerable<(Guid Id, int Ordinal, string Text)> chunks)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        using (var docCommand = connection.CreateCommand())
        {
            docCommand.CommandText =
                @"INSERT OR IGNORE INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
                  VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt);";
            docCommand.Parameters.AddWithValue("@id", documentId.ToString());
            docCommand.Parameters.AddWithValue("@filename", "detail-doc.txt");
            docCommand.Parameters.AddWithValue("@mime", "text/plain");
            docCommand.Parameters.AddWithValue("@size", 64);
            docCommand.Parameters.AddWithValue("@hash", $"detail-{documentId:N}");
            docCommand.Parameters.AddWithValue("@status", "Ready");
            docCommand.Parameters.AddWithValue("@createdBy", "seed");
            docCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            await docCommand.ExecuteNonQueryAsync();
        }

        foreach (var chunk in chunks)
        {
            using var chunkCommand = connection.CreateCommand();
            chunkCommand.CommandText =
                @"INSERT OR IGNORE INTO Chunks (Id, DocumentId, Ordinal, Text, TokenCount)
                  VALUES (@id, @documentId, @ordinal, @text, @tokens);";
            chunkCommand.Parameters.AddWithValue("@id", chunk.Id.ToString());
            chunkCommand.Parameters.AddWithValue("@documentId", documentId.ToString());
            chunkCommand.Parameters.AddWithValue("@ordinal", chunk.Ordinal);
            chunkCommand.Parameters.AddWithValue("@text", chunk.Text);
            chunkCommand.Parameters.AddWithValue("@tokens", chunk.Text.Length);
            await chunkCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task<QueryDetailShape> ParseDetailAsync(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return new QueryDetailShape
        {
            Id = root.GetProperty("id").GetString()!,
            Prompt = root.GetProperty("prompt").GetString()!,
            Answer = root.GetProperty("answer").GetString()!,
            Citations = root.GetProperty("citations")
                .EnumerateArray()
                .Select(c => new QueryCitationShape
                {
                    DocumentId = c.GetProperty("documentId").GetString()!,
                    ChunkId = c.GetProperty("chunkId").GetString()!,
                    Text = c.GetProperty("text").GetString()!,
                    Ordinal = c.GetProperty("ordinal").GetInt32(),
                })
                .ToList(),
        };
    }

    private static async Task<HistoryPageShape> ParsePageAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return new HistoryPageShape
        {
            Total = root.GetProperty("total").GetInt32(),
            Limit = root.GetProperty("limit").GetInt32(),
            Offset = root.GetProperty("offset").GetInt32(),
            Items = root.GetProperty("items")
                .EnumerateArray()
                .Select(i => new HistoryItemShape
                {
                    Id = i.GetProperty("id").GetString()!,
                    PromptPreview = i.GetProperty("promptPreview").GetString()!,
                    AnswerPreview = i.GetProperty("answerPreview").GetString()!,
                    CitationCount = i.GetProperty("citationCount").GetInt32(),
                    CreatedAt = i.GetProperty("createdAt").GetString()!,
                })
                .ToList(),
        };
    }

    private sealed class HistoryPageShape
    {
        public int Total { get; set; }

        public int Limit { get; set; }

        public int Offset { get; set; }

        public List<HistoryItemShape> Items { get; set; } = new();
    }

    private sealed class HistoryItemShape
    {
        public string Id { get; set; } = string.Empty;

        public string PromptPreview { get; set; } = string.Empty;

        public string AnswerPreview { get; set; } = string.Empty;

        public int CitationCount { get; set; }

        public string CreatedAt { get; set; } = string.Empty;
    }

    private sealed class QueryDetailShape
    {
        public string Id { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        public List<QueryCitationShape> Citations { get; set; } = new();
    }

    private sealed class QueryCitationShape
    {
        public string DocumentId { get; set; } = string.Empty;

        public string ChunkId { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int Ordinal { get; set; }
    }
}
