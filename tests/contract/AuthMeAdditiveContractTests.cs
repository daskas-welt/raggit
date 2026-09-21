using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T016: Contract tests for GET /api/auth/me additive 1.3.0 envelope.
/// </summary>
public sealed class AuthMeAdditiveContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthMeAdditiveContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Me_LocalBearer_ReturnsAdditiveIdentityEnvelope()
    {
        var user = await CreateUserAsync("me-local", "Me Local", UserRole.Employee, "me-pass-1");
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, user.Username, "me-pass-1");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("identityType").GetString().Should().Be("Local");
        doc.RootElement.GetProperty("role").GetString().Should().Be("Employee");
        doc.RootElement.GetProperty("displayName").GetString().Should().Be(user.DisplayName);
        doc.RootElement.GetProperty("username").GetString().Should().Be(user.Username);
        var sub = doc.RootElement.GetProperty("sub").GetString();
        Guid.TryParse(sub, out var subGuid).Should().BeTrue();
        subGuid.Should().Be(user.Id);
    }

    [Fact]
    public async Task Me_ApiKey_Returns120ShapeWithoutAdditiveFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("identityType").GetString().Should().Be("ApiKey");
        doc.RootElement.GetProperty("role").GetString().Should().Be("Admin");
        doc.RootElement.TryGetProperty("displayName", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("username", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("sub", out _).Should().BeFalse();
    }

    private async Task<User> CreateUserAsync(
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
        return user;
    }

    private async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
