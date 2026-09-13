using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T026: Deactivating an account refuses its very next request and allows zero further successes.
/// </summary>
public sealed class DeactivationRefusalTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public DeactivationRefusalTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeactivateAccount_VeryNextBearerRequest_Returns401_AndZeroSuccesses()
    {
        var user = await ProvisionAndLoginAsync(
            "deact-victim",
            "Deact Victim",
            UserRole.Employee,
            "deact-pass-1"
        );
        var adminClient = CreateAdminClient();

        // Verify the active session works.
        var activeMe = await GetAuthMeAsync(user.Token);
        activeMe.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin deactivates the account.
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { isActive = false }
        );
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        // Very next request with the still-valid JWT must be refused.
        var refusedMe = await GetAuthMeAsync(user.Token);
        refusedMe.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Repeated requests continue to be refused — zero further successes.
        for (var i = 0; i < 5; i++)
        {
            var retry = await GetAuthMeAsync(user.Token);
            retry
                .StatusCode.Should()
                .Be(HttpStatusCode.Unauthorized, $"attempt {i + 1} after deactivation");
        }
    }

    [Fact]
    public async Task DeactivateAccount_PreventsFreshLogin()
    {
        await ProvisionAndLoginAsync(
            "deact-login",
            "Deact Login",
            UserRole.Employee,
            "deact-login-pass-1"
        );
        var adminClient = CreateAdminClient();

        var user = await GetUserByUsernameAsync("deact-login");
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { isActive = false }
        );
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await _factory
            .CreateClient()
            .PostAsJsonAsync(
                "/api/auth/login",
                new { username = "deact-login", password = "deact-login-pass-1" }
            );
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid Id, string Token)> ProvisionAndLoginAsync(
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
        return (user.Id, token);
    }

    private async Task<User> GetUserByUsernameAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<UserStore>();
        var user = await store.GetByUsernameAsync(username);
        return user ?? throw new InvalidOperationException($"User {username} not found.");
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);
        return client;
    }

    private async Task<HttpResponseMessage> GetAuthMeAsync(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/api/auth/me");
    }
}
