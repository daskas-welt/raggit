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
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T029 [US5]: History and mine work offline (WAN logically off) — SC-004.
/// Seeds 2 users × queries + uploads, snapshots GET /api/queries/history and
/// GET /api/documents/mine, then sabotages the LLM fake (Ollama unreachable,
/// the WAN-disabled condition) and asserts byte-identical totals/items.
/// CI also runs this class under `unshare -n` (see .github/workflows/ci.yml).
/// </summary>
public sealed class OfflineHistoryTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public OfflineHistoryTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HistoryAndMine_WanDisabled_ReturnSameTotalAndItems()
    {
        var (admin, adminToken) = await ProvisionAndLoginAsync(
            $"off-ada-{Guid.NewGuid():N}",
            "Offline Ada",
            UserRole.Admin,
            "off-ada-pass-1"
        );
        var (employee, employeeToken) = await ProvisionAndLoginAsync(
            $"off-bob-{Guid.NewGuid():N}",
            "Offline Bob",
            UserRole.Employee,
            "off-bob-pass-1"
        );

        var baseTime = DateTime.UtcNow;
        await SeedQueriesAsync(
            admin.Id.ToString(),
            Enumerable
                .Range(0, 3)
                .Select(i => (baseTime.AddSeconds(-i), $"off ada query {i}", "off ada answer"))
        );
        await SeedQueriesAsync(
            employee.Id.ToString(),
            Enumerable
                .Range(0, 2)
                .Select(i => (baseTime.AddSeconds(-i), $"off bob query {i}", "off bob answer"))
        );
        await SeedDocumentsAsync(admin.Id.ToString(), new[] { "off-ada-1.txt", "off-ada-2.txt" });
        await SeedDocumentsAsync(employee.Id.ToString(), new[] { "off-bob-1.txt" });

        var adminClient = CreateBearerClient(adminToken);
        var employeeClient = CreateBearerClient(employeeToken);

        // Pass 1 ("WAN-enabled"): snapshot both endpoints for both users.
        var adminHistoryOn = await GetBodyAsync(
            adminClient,
            "/api/queries/history?limit=100&offset=0"
        );
        var adminMineOn = await GetBodyAsync(adminClient, "/api/documents/mine?limit=100&offset=0");
        var employeeHistoryOn = await GetBodyAsync(
            employeeClient,
            "/api/queries/history?limit=100&offset=0"
        );
        var employeeMineOn = await GetBodyAsync(
            employeeClient,
            "/api/documents/mine?limit=100&offset=0"
        );

        AssertSnapshot(adminHistoryOn, total: 3, expectedCount: 3);
        AssertSnapshot(adminMineOn, total: 2, expectedCount: 2);
        AssertSnapshot(employeeHistoryOn, total: 2, expectedCount: 2);
        AssertSnapshot(employeeMineOn, total: 1, expectedCount: 1);

        // Pass 2 ("WAN-disabled"): Ollama unreachable — history/mine must be
        // unaffected because both stores are pure SQLite reads (FR-009).
        var llm = _factory.Services.GetRequiredService<ILlmClient>();
        llm.Should().BeOfType<FakeLlmClient>();
        var fake = (FakeLlmClient)llm;
        fake.Healthy = false;
        fake.ThrowOnChat = true;
        try
        {
            var adminHistoryOff = await GetBodyAsync(
                adminClient,
                "/api/queries/history?limit=100&offset=0"
            );
            var adminMineOff = await GetBodyAsync(
                adminClient,
                "/api/documents/mine?limit=100&offset=0"
            );
            var employeeHistoryOff = await GetBodyAsync(
                employeeClient,
                "/api/queries/history?limit=100&offset=0"
            );
            var employeeMineOff = await GetBodyAsync(
                employeeClient,
                "/api/documents/mine?limit=100&offset=0"
            );

            // SC-004: same data, no degradation, zero egress (no LLM call possible).
            adminHistoryOff.Should().Be(adminHistoryOn);
            adminMineOff.Should().Be(adminMineOn);
            employeeHistoryOff.Should().Be(employeeHistoryOn);
            employeeMineOff.Should().Be(employeeMineOn);
        }
        finally
        {
            fake.Healthy = true;
            fake.ThrowOnChat = false;
        }
    }

    private static void AssertSnapshot(string body, int total, int expectedCount)
    {
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("total").GetInt32().Should().Be(total);
        doc.RootElement.GetProperty("items").EnumerateArray().Should().HaveCount(expectedCount);
    }

    private static async Task<string> GetBodyAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private HttpClient CreateBearerClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(User User, string Token)> ProvisionAndLoginAsync(
        string username,
        string displayName,
        UserRole role,
        string password
    )
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(password),
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
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user, doc.RootElement.GetProperty("access_token").GetString()!);
    }

    private async Task SeedQueriesAsync(
        string sub,
        IEnumerable<(DateTime CreatedAt, string Prompt, string Answer)> rows
    )
    {
        using var scope = _factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
        foreach (var row in rows)
        {
            await queries.AddAsync(
                new Query
                {
                    Id = Guid.NewGuid(),
                    UserId = sub,
                    Prompt = row.Prompt,
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = row.Answer,
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 5,
                    CreatedAt = row.CreatedAt,
                }
            );
        }
    }

    private async Task SeedDocumentsAsync(string sub, IEnumerable<string> filenames)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        foreach (var filename in filenames)
        {
            var id = Guid.NewGuid();
            using var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
                  VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt);";
            command.Parameters.AddWithValue("@id", id.ToString());
            command.Parameters.AddWithValue("@filename", filename);
            command.Parameters.AddWithValue("@mime", "text/plain");
            command.Parameters.AddWithValue("@size", 64);
            command.Parameters.AddWithValue("@hash", $"off-mine-{id:N}");
            command.Parameters.AddWithValue("@status", "Ready");
            command.Parameters.AddWithValue("@createdBy", sub);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
    }
}
