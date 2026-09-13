using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T017: Authenticated actions are attributed to the stable person id.
/// </summary>
public sealed class IdentityAttributionTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public IdentityAttributionTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Attribution_LocalSession_AdminUpload_And_EmployeeQuery()
    {
        var (ada, adaToken) = await ProvisionAndLoginAsync(
            "ada",
            "Ada Lovelace",
            UserRole.Admin,
            "ada-pass-1"
        );
        var (bob, bobToken) = await ProvisionAndLoginAsync(
            "bob",
            "Bob Moore",
            UserRole.Employee,
            "bob-pass-1"
        );

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adaToken
        );

        var upload = await UploadTextAsync(
            adminClient,
            "attribution.txt",
            "This is Ada's document."
        );
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(upload);
        document.CreatedBy.Should().Be(ada.Id.ToString());

        var employeeClient = _factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            bobToken
        );

        var queryResponse = await employeeClient.PostAsJsonAsync(
            "/api/query",
            new { query = "what is in the document?", topK = 5 }
        );
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var queryUserId = await GetLatestQueryUserIdAsync();
        queryUserId.Should().Be(bob.Id.ToString());
    }

    [Fact]
    public async Task Attribution_EmployeeUpload_Returns403()
    {
        var (_, bobToken) = await ProvisionAndLoginAsync(
            "bob-upload",
            "Bob Upload",
            UserRole.Employee,
            "bob-upload-1"
        );

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            bobToken
        );

        var upload = await UploadTextAsync(
            client,
            "employee-attempt.txt",
            "Employee trying to upload."
        );
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("access_token").GetString()!;
        return (user, token);
    }

    private async Task<string> GetLatestQueryUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserId FROM Queries ORDER BY CreatedAt DESC LIMIT 1;";
        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    private static async Task<HttpResponseMessage> UploadTextAsync(
        HttpClient client,
        string filename,
        string text
    )
    {
        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", filename);
        return await client.PostAsync("/api/documents", form);
    }

    private static async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };
}
