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
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;
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
    public async Task Detail_NonOwnerSeesNothing_ZeroLeakAcrossRoles()
    {
        // FR-007/FR-012/SC-002: ≥2 users, ≥20 mixed actions; neither history nor
        // detail leaks a row across persons, regardless of role (Admin own-only).
        var seed = await SeedTwoUsersAsync();
        var adminClient = CreateBearerClient(seed.AdminToken);
        var employeeClient = CreateBearerClient(seed.EmployeeToken);

        var adminPage = await GetHistoryAsync(adminClient);
        var employeePage = await GetHistoryAsync(employeeClient);

        var allAdminIds = adminPage.Items.Select(i => i.Id).ToList();
        var allEmployeeIds = employeePage.Items.Select(i => i.Id).ToList();

        // Disjoint id sets: zero cross-user rows on history.
        allAdminIds.Should().NotIntersectWith(allEmployeeIds);

        // Detail: each person's rows 404 for the other person (never 200, never 403).
        foreach (var id in allAdminIds.Take(3))
        {
            (await employeeClient.GetAsync($"/api/queries/{id}"))
                .StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }

        foreach (var id in allEmployeeIds.Take(3))
        {
            (await adminClient.GetAsync($"/api/queries/{id}"))
                .StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }

        // Detail: own rows still 200 (isolation did not over-block).
        (await adminClient.GetAsync($"/api/queries/{allAdminIds.First()}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await employeeClient.GetAsync($"/api/queries/{allEmployeeIds.First()}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
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
            var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
            await queries.AddAsync(
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

    [Fact]
    public async Task History_LegacyRows_NeverAppear_ForAnyPerson()
    {
        // FR-008/SC-005: legacy API-key queries (UserId admin/employee) stay in
        // the DB but are excluded from every person's history and detail.
        var seed = await SeedTwoUsersAsync();
        var legacyAdminId = Guid.NewGuid();
        var legacyEmployeeId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
            await queries.AddAsync(
                new Query
                {
                    Id = legacyAdminId,
                    UserId = "admin",
                    Prompt = "legacy admin prompt",
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = "legacy admin answer",
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            );
            await queries.AddAsync(
                new Query
                {
                    Id = legacyEmployeeId,
                    UserId = "employee",
                    Prompt = "legacy employee prompt",
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = "legacy employee answer",
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 1,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        foreach (var token in new[] { seed.AdminToken, seed.EmployeeToken })
        {
            var client = CreateBearerClient(token);
            var page = await GetHistoryAsync(client);
            page.Items.Should().NotContain(i => i.Id == legacyAdminId.ToString());
            page.Items.Should().NotContain(i => i.Id == legacyEmployeeId.ToString());
            page.Items.Should().OnlyContain(i => !i.Prompt.StartsWith("legacy "));

            (await client.GetAsync($"/api/queries/{legacyAdminId}"))
                .StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"/api/queries/{legacyEmployeeId}"))
                .StatusCode.Should()
                .Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task HistoryAndDetail_ExpiredToken_Returns401_NoData()
    {
        // T022: expired JWT is rejected by JwtBearer lifetime validation —
        // history and detail return 401 with no rows, never 200/404.
        var (user, _) = await ProvisionAndLoginAsync(
            $"iso-exp-{Guid.NewGuid():N}",
            "Iso Expired",
            UserRole.Employee,
            "iso-exp-pass-1"
        );
        var queryId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
            await queries.AddAsync(
                new Query
                {
                    Id = queryId,
                    UserId = user.Id.ToString(),
                    Prompt = "expired prompt",
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = "expired answer",
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 2,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        var expiredClient = CreateBearerClient(IssueExpiredToken(user));

        (await expiredClient.GetAsync("/api/queries/history?limit=20&offset=0"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
        (await expiredClient.GetAsync($"/api/queries/{queryId}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HistoryAndDetail_DeactivatedMidBrowse_NextRequestsAre401()
    {
        // T022: OnTokenValidated refuses the deactivated account on its very
        // next request — history page and detail both 401, no data returned.
        var (user, token) = await ProvisionAndLoginAsync(
            $"iso-deact-{Guid.NewGuid():N}",
            "Iso Deactivated",
            UserRole.Employee,
            "iso-deact-pass-1"
        );
        var queryId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
            await queries.AddAsync(
                new Query
                {
                    Id = queryId,
                    UserId = user.Id.ToString(),
                    Prompt = "deact prompt",
                    RetrievedChunkIds = Array.Empty<Guid>(),
                    Answer = "deact answer",
                    CitationIds = Array.Empty<Guid>(),
                    LatencyMs = 2,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        var client = CreateBearerClient(token);
        (await client.GetAsync("/api/queries/history?limit=20&offset=0"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { isActive = false }
        );
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetAsync("/api/queries/history?limit=20&offset=0"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/queries/{queryId}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_EachPerson_SeesOnlyOwnDocuments()
    {
        // T024 [US3] FR-005/FR-007: ≥2 users with uploads; GET /api/documents/mine
        // returns only rows where CreatedBy == sub — zero cross-user leakage.
        var (admin, adminToken) = await ProvisionAndLoginAsync(
            $"iso-mine-ada-{Guid.NewGuid():N}",
            "Iso Mine Ada",
            UserRole.Admin,
            "iso-mine-ada-pass-1"
        );
        var (employee, employeeToken) = await ProvisionAndLoginAsync(
            $"iso-mine-bob-{Guid.NewGuid():N}",
            "Iso Mine Bob",
            UserRole.Employee,
            "iso-mine-bob-pass-1"
        );

        await SeedDocumentsAsync(
            admin.Id.ToString(),
            new[] { "ada-doc-one.txt", "ada-doc-two.txt" }
        );
        await SeedDocumentsAsync(employee.Id.ToString(), new[] { "bob-doc-one.txt" });

        var adminPage = await GetMineAsync(CreateBearerClient(adminToken));
        var employeePage = await GetMineAsync(CreateBearerClient(employeeToken));

        adminPage.Total.Should().Be(2);
        adminPage.Items.Should().HaveCount(2);
        adminPage.Items.Should().OnlyContain(i => i.Filename.StartsWith("ada-doc"));

        employeePage.Total.Should().Be(1);
        employeePage.Items.Should().ContainSingle().Which.Filename.Should().Be("bob-doc-one.txt");
    }

    private string IssueExpiredToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var existingOptions = scope
            .ServiceProvider.GetRequiredService<IOptions<JwtTokenServiceOptions>>()
            .Value;

        var expiredOptions = Options.Create(
            new JwtTokenServiceOptions
            {
                SigningKey = existingOptions.SigningKey,
                TokenLifetimeHours = -1,
            }
        );
        return new JwtTokenService(expiredOptions).IssueToken(user);
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
        // Nonce the bytes so a second seed in the shared test DB does not hit
        // the content-hash dedupe path (which returns 200 instead of 201).
        var nonce = Guid.NewGuid().ToString("N");
        var adminClient = CreateBearerClient(adminToken);
        (await UploadTextAsync(adminClient, $"iso-admin-1-{nonce}.txt", $"Admin doc one {nonce}."))
            .StatusCode.Should()
            .Be(HttpStatusCode.Created);
        (await UploadTextAsync(adminClient, $"iso-admin-2-{nonce}.txt", $"Admin doc two {nonce}."))
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
            command.Parameters.AddWithValue("@hash", $"iso-mine-{id:N}");
            command.Parameters.AddWithValue("@status", "Ready");
            command.Parameters.AddWithValue("@createdBy", sub);
            command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
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

    private static async Task<MineShape> GetMineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents/mine?limit=100&offset=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new MineShape
        {
            Total = doc.RootElement.GetProperty("total").GetInt32(),
            Items = doc
                .RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(i => new MineRow
                {
                    Id = i.GetProperty("id").GetString()!,
                    Filename = i.GetProperty("filename").GetString()!,
                })
                .ToList(),
        };
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

    private sealed class MineShape
    {
        public int Total { get; set; }

        public List<MineRow> Items { get; set; } = new();
    }

    private sealed class MineRow
    {
        public string Id { get; set; } = string.Empty;

        public string Filename { get; set; } = string.Empty;
    }
}
