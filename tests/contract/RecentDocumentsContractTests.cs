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
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T023: Contract tests for GET /api/documents/mine per
/// specs/005-per-person-history/contracts/api.yaml (1.4.0).
/// Own-only (CreatedBy == sub, legacy excluded), stable order
/// CreatedAt DESC, Id DESC, limit/offset clamp, 401 without token.
/// </summary>
public sealed class RecentDocumentsContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public RecentDocumentsContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Mine_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents/mine?limit=20&offset=0");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_NewUser_ReturnsEmptyPage()
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"mine-empty-{Guid.NewGuid():N}",
            UserRole.Employee
        );

        var client = CreateBearerClient(token);
        var response = await client.GetAsync("/api/documents/mine?limit=20&offset=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ParsePageAsync(response);
        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
        page.Limit.Should().Be(20);
        page.Offset.Should().Be(0);
    }

    [Fact]
    public async Task Mine_ReturnsOwnOnly_MostRecentFirst()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"mine-own-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        var otherSub = Guid.NewGuid().ToString();
        var baseTime = DateTime.UtcNow;
        await SeedDocumentsAsync(
            sub,
            new[]
            {
                (baseTime.AddMinutes(-2), "own-old.txt"),
                (baseTime.AddMinutes(-1), "own-mid.txt"),
                (baseTime, "own-new.txt"),
            }
        );
        await SeedDocumentsAsync(otherSub, new[] { (baseTime, "other-doc.txt") });

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(await client.GetAsync("/api/documents/mine"));

        page.Total.Should().Be(3);
        page.Limit.Should().Be(20);
        page.Offset.Should().Be(0);
        page.Items.Should().HaveCount(3);
        page.Items.Select(i => i.Filename)
            .Should()
            .ContainInOrder("own-new.txt", "own-mid.txt", "own-old.txt");
        page.Items.Should().OnlyContain(i => !i.Filename.Contains("other-doc"));
    }

    [Theory]
    [InlineData("limit=0", 20)]
    [InlineData("limit=500", 100)]
    [InlineData("limit=-3", 20)]
    public async Task Mine_LimitOutOfRange_IsClamped(string query, int expectedLimit)
    {
        var (_, token) = await ProvisionAndLoginAsync(
            $"mine-clamp-{Guid.NewGuid():N}",
            UserRole.Employee
        );

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(await client.GetAsync($"/api/documents/mine?{query}"));

        page.Limit.Should().Be(expectedLimit);
    }

    [Fact]
    public async Task Mine_OffsetBeyondTotal_ReturnsEmpty_WithTotal()
    {
        var (sub, token) = await ProvisionAndLoginAsync(
            $"mine-offset-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        await SeedDocumentsAsync(sub, new[] { (DateTime.UtcNow, "solo-doc.txt") });

        var client = CreateBearerClient(token);
        var page = await ParsePageAsync(
            await client.GetAsync("/api/documents/mine?limit=20&offset=99")
        );

        page.Total.Should().Be(1);
        page.Items.Should().BeEmpty();
        page.Offset.Should().Be(99);
    }

    [Fact]
    public async Task Mine_Body_NeverContainsPasswordHashOrCreatedBy()
    {
        // T034: mine projection must never serialize credentials or owner keys.
        var (sub, token) = await ProvisionAndLoginAsync(
            $"mine-noleak-{Guid.NewGuid():N}",
            UserRole.Employee
        );
        await SeedDocumentsAsync(sub, new[] { (DateTime.UtcNow, "noleak-doc.txt") });

        var client = CreateBearerClient(token);
        var body = await (await client.GetAsync("/api/documents/mine")).Content.ReadAsStringAsync();

        body.ToLowerInvariant().Should().NotContain("passwordhash");
        body.ToLowerInvariant().Should().NotContain("createdby");
        body.Should().NotContain(sub);
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
        var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword("mine-pass-1"),
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
            new { username, password = "mine-pass-1" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user.Id.ToString(), doc.RootElement.GetProperty("access_token").GetString()!);
    }

    private async Task SeedDocumentsAsync(
        string sub,
        IEnumerable<(DateTime CreatedAt, string Filename)> rows
    )
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        foreach (var row in rows)
        {
            var id = Guid.NewGuid();
            using var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
                  VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt);";
            command.Parameters.AddWithValue("@id", id.ToString());
            command.Parameters.AddWithValue("@filename", row.Filename);
            command.Parameters.AddWithValue("@mime", "text/plain");
            command.Parameters.AddWithValue("@size", 128);
            command.Parameters.AddWithValue("@hash", $"mine-{id:N}");
            command.Parameters.AddWithValue("@status", "Ready");
            command.Parameters.AddWithValue("@createdBy", sub);
            command.Parameters.AddWithValue("@createdAt", row.CreatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<MinePageShape> ParsePageAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        return new MinePageShape
        {
            Total = root.GetProperty("total").GetInt32(),
            Limit = root.GetProperty("limit").GetInt32(),
            Offset = root.GetProperty("offset").GetInt32(),
            Items = root.GetProperty("items")
                .EnumerateArray()
                .Select(i => new MineItemShape
                {
                    Id = i.GetProperty("id").GetString()!,
                    Filename = i.GetProperty("filename").GetString()!,
                    Size = i.GetProperty("size").GetInt64(),
                    Status = i.GetProperty("status").GetString()!,
                    CreatedAt = i.GetProperty("createdAt").GetString()!,
                })
                .ToList(),
        };
    }

    private sealed class MinePageShape
    {
        public int Total { get; set; }

        public int Limit { get; set; }

        public int Offset { get; set; }

        public List<MineItemShape> Items { get; set; } = new();
    }

    private sealed class MineItemShape
    {
        public string Id { get; set; } = string.Empty;

        public string Filename { get; set; } = string.Empty;

        public long Size { get; set; }

        public string Status { get; set; } = string.Empty;

        public string CreatedAt { get; set; } = string.Empty;
    }
}
