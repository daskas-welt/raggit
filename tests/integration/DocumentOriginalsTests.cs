using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Integration tests for 009-library-item-actions: stored-original purge on
/// delete and uploader display-name attribution. Uses the real Workstation.Api
/// with temp SQLite/LanceDB and deterministic fakes for Ollama.
/// </summary>
public sealed class DocumentOriginalsTests
{
    [Fact]
    public async Task Delete_PurgesStoredOriginal()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);

        var uploaded = await UploadTextAsync(adminClient, "purge-original.txt", "Purge me.");
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await ReadDocumentIdAsync(uploaded);

        var before = await adminClient.GetAsync($"/api/documents/{documentId}/content");
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await adminClient.DeleteAsync($"/api/documents/{documentId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await adminClient.GetAsync($"/api/documents/{documentId}/content");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_AttributesDisplayName()
    {
        using var factory = new IntegrationTestFactory();
        var (user, token) = await ProvisionAndLoginAsync(
            factory,
            $"orig-ada-{Guid.NewGuid():N}",
            "Content Ada",
            UserRole.Admin,
            "orig-ada-pass-1"
        );
        var bearerClient = factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var uploaded = await UploadTextAsync(bearerClient, "attributed.txt", "Attribute me.");
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await ReadDocumentIdAsync(uploaded);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CreatedBy, CreatedByName FROM Documents WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", documentId);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be(user.Id.ToString());
        reader.IsDBNull(1).Should().BeFalse();
        reader.GetString(1).Should().Be("Content Ada");
    }

    private static HttpClient CreateAdminClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        return client;
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

    private static async Task<string> ReadDocumentIdAsync(HttpResponseMessage response)
    {
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Upload response has no id.");
    }

    private static async Task<(User User, string Token)> ProvisionAndLoginAsync(
        IntegrationTestFactory factory,
        string username,
        string displayName,
        UserRole role,
        string password
    )
    {
        using var scope = factory.Services.CreateScope();
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

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user, doc.RootElement.GetProperty("access_token").GetString()!);
    }
}
