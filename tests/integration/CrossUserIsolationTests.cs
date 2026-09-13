using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T007/T019/T020: Cross-user isolation for per-person history (005-per-person-history).
/// Seeds ≥2 users with ≥20 mixed actions (queries + uploads) and asserts zero leakage.
/// </summary>
public sealed class CrossUserIsolationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public CrossUserIsolationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task History_EachPerson_SeesOnlyOwnQueries()
    {
        var seed = await SeedTwoUsersAsync();
        var adminClient = CreateBearerClient(seed.AdminToken);
        var employeeClient = CreateBearerClient(seed.EmployeeToken);

        var adminPage = await GetHistoryAsync(adminClient);
        var employeePage = await GetHistoryAsync(employeeClient);

        adminPage.Total.Should().Be(seed.AdminQueryCount);
        adminPage.Items.Should().HaveCount(seed.AdminQueryCount);
        adminPage.Items.Should().OnlyContain(i => i.Prompt.StartsWith("ada query"));

        employeePage.Total.Should().Be(seed.EmployeeQueryCount);
        employeePage.Items.Should().HaveCount(seed.EmployeeQueryCount);
        employeePage.Items.Should().OnlyContain(i => i.Prompt.StartsWith("bob query"));
    }

    [Fact]
    public async Task Detail_QueryWithNoRelevantContent_ReturnsEmptyCitations()
    {
        var (_, employeeToken) = await ProvisionAndLoginAsync(
            $"iso-nocite-{Guid.NewGuid():N}",
            "Iso NoCite",
            UserRole.Employee,
            "iso-nocite-pass-1"
        );

        var queryId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
            await db.InsertQueryAsync(
                new Query
                {
                    Id = queryId,
                    UserId = await GetSubFromTokenAsync(employeeToken),
                    Prompt = "unanswerable question",
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = "no relevant content found",
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 3,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        var client = CreateBearerClient(employeeToken);
        var response = await client.GetAsync($"/api/queries/{queryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("answer").GetString().Should().Be("no relevant content found");
        doc.RootElement.GetProperty("citations").EnumerateArray().Should().BeEmpty();
    }

    private async Task<string> GetSubFromTokenAsync(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("sub").GetString()!;
    }

    private async Task<IsolationSeed> SeedTwoUsersAsync()
    {
        var (admin, adminToken) = await ProvisionAndLoginAsync(
            $"iso-ada-{Guid.NewGuid():N}",
            "Iso Ada",
            UserRole.Admin,
            "iso-ada-pass-1"
        );
        var (employee, employeeToken) = await ProvisionAndLoginAsync(
            $"iso-bob-{Guid.NewGuid():N}",
            "Iso Bob",
            UserRole.Employee,
            "iso-bob-pass-1"
        );

        // Mixed actions: admin uploads 2 documents (admin-only endpoint).
        var adminClient = CreateBearerClient(adminToken);
        (await UploadTextAsync(adminClient, "iso-admin-1.txt", "Admin doc one."))
            .StatusCode.Should()
            .Be(HttpStatusCode.Created);
        (await UploadTextAsync(adminClient, "iso-admin-2.txt", "Admin doc two."))
            .StatusCode.Should()
            .Be(HttpStatusCode.Created);

        // Mixed actions: 10 admin + 12 employee queries (22 queries + 2 uploads = 24 ≥ 20).
        const int adminQueryCount = 10;
        const int employeeQueryCount = 12;
        var baseTime = DateTime.UtcNow;
        await SeedQueriesAsync(
            admin.Id.ToString(),
            Enumerable
                .Range(0, adminQueryCount)
                .Select(i => (baseTime.AddSeconds(-i), $"ada query {i}", "ada answer"))
        );
        await SeedQueriesAsync(
            employee.Id.ToString(),
            Enumerable
                .Range(0, employeeQueryCount)
                .Select(i => (baseTime.AddSeconds(-i), $"bob query {i}", "bob answer"))
        );

        return new IsolationSeed(adminToken, employeeToken, adminQueryCount, employeeQueryCount);
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
        var store = scope.ServiceProvider.GetRequiredService<UserStore>();
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
                    LatencyMs = 5,
                    CreatedAt = row.CreatedAt,
                }
            );
        }
    }

    private static async Task<HttpResponseMessage> UploadTextAsync(
        HttpClient client,
        string filename,
        string text
    )
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", filename);
        return await client.PostAsync("/api/documents", form);
    }

    private static async Task<HistoryShape> GetHistoryAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/queries/history?limit=100&offset=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new HistoryShape
        {
            Total = doc.RootElement.GetProperty("total").GetInt32(),
            Items = doc
                .RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(i => new HistoryRow
                {
                    Id = i.GetProperty("id").GetString()!,
                    Prompt = i.GetProperty("promptPreview").GetString()!,
                })
                .ToList(),
        };
    }

    private sealed record IsolationSeed(
        string AdminToken,
        string EmployeeToken,
        int AdminQueryCount,
        int EmployeeQueryCount
    );

    private sealed class HistoryShape
    {
        public int Total { get; set; }

        public List<HistoryRow> Items { get; set; } = new();
    }

    private sealed class HistoryRow
    {
        public string Id { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;
    }
}
