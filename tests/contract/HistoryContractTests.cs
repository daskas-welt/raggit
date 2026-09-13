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
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        foreach (var row in rows)
        {
            await db.InsertQueryAsync(
                new Query
                {
                    Id = Guid.NewGuid(),
                    UserId = sub,
                    Prompt = row.Prompt,
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = row.Answer,
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 7,
                    CreatedAt = row.CreatedAt,
                }
            );
        }
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
}
