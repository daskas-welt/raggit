using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T033: Client session lifecycle — expiry → 401 → re-login prompt; sign-out → anonymous.
/// </summary>
public sealed class SessionLifecycleTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public SessionLifecycleTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExpiredToken_NextAuthMe_Returns401_ThenFreshLogin_Succeeds()
    {
        var (user, _) = await ProvisionAndLoginAsync(
            "session-expiry",
            "Session Expiry",
            UserRole.Employee,
            "expiry-pass-1"
        );

        // Issue an expired token from the same signing key (simulates stale cache).
        var expiredToken = IssueExpiredToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            expiredToken
        );

        var me = await client.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Re-login prompt path: fresh credentials yield a new valid token.
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "expiry-pass-1" }
        );
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SignOut_SubsequentRequests_AreAnonymous401()
    {
        var (_, token) = await ProvisionAndLoginAsync(
            "session-signout",
            "Session Signout",
            UserRole.Employee,
            "signout-pass-1"
        );

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        var beforeSignOut = await authenticatedClient.GetAsync("/api/auth/me");
        beforeSignOut.StatusCode.Should().Be(HttpStatusCode.OK);

        // Sign-out is a client-side cache clear: subsequent calls carry no credential.
        var anonymousClient = _factory.CreateClient();
        var afterSignOut = await anonymousClient.GetAsync("/api/auth/me");
        afterSignOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
        var token = doc.RootElement.GetProperty("access_token").GetString()!;
        return (user, token);
    }

    private string IssueExpiredToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var existingOptions = scope
            .ServiceProvider.GetRequiredService<IOptions<JwtTokenServiceOptions>>()
            .Value;

        // Issue with a negative lifetime so the token is already expired.
        var expiredOptions = Options.Create(
            new JwtTokenServiceOptions
            {
                SigningKey = existingOptions.SigningKey,
                TokenLifetimeHours = -1,
            }
        );
        var tokenService = new JwtTokenService(expiredOptions);
        return tokenService.IssueToken(user);
    }
}
