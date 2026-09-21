using System;
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
/// T036: Contract tests for POST /api/auth/refresh.
/// </summary>
public sealed class AuthRefreshContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthRefreshContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewBearerTokenResponse()
    {
        var user = await CreateUserAsync(
            "refresh-ok",
            "Refresh OK",
            UserRole.Employee,
            "refresh-pass-1"
        );
        var token = await LoginAsync(user.Username, "refresh-pass-1");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
        doc.RootElement.GetProperty("expires_in").GetInt32().Should().Be(28_800);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var user = await CreateUserAsync(
            "refresh-expired",
            "Refresh Expired",
            UserRole.Employee,
            "refresh-expired-pass-1"
        );
        var token = IssueExpiredToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_NoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_DeactivatedAccount_Returns401()
    {
        var user = await CreateUserAsync(
            "refresh-deactivated",
            "Refresh Deactivated",
            UserRole.Employee,
            "refresh-deact-pass-1"
        );
        var token = await LoginAsync(user.Username, "refresh-deact-pass-1");

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            user.IsActive = false;
            await store.UpdateAsync(user);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/auth/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private async Task<string> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private string IssueExpiredToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var key =
            scope.ServiceProvider.GetRequiredService<RAGGit.Workstation.Api.Auth.JwtTokenService>();
        // Abuse the same signing key with a negative lifetime to mint an expired token.
        var options = Microsoft.Extensions.Options.Options.Create(
            new RAGGit.Workstation.Api.Auth.JwtTokenServiceOptions
            {
                SigningKey = scope
                    .ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RAGGit.Workstation.Api.Auth.JwtTokenServiceOptions>>()
                    .Value.SigningKey,
                TokenLifetimeHours = -1,
            }
        );
        var expiredService = new RAGGit.Workstation.Api.Auth.JwtTokenService(options);
        return expiredService.IssueToken(user);
    }
}
