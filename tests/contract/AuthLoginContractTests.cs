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
/// T015: Contract tests for POST /api/auth/login per contracts/api.yaml 1.3.0.
/// </summary>
public sealed class AuthLoginContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthLoginContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsBearerTokenResponse()
    {
        var user = await CreateUserAsync("login-ok", "Login OK", UserRole.Admin, "valid-pass-1");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "valid-pass-1" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
        doc.RootElement.GetProperty("expires_in").GetInt32().Should().Be(28_800);
    }

    [Fact]
    public async Task Login_UnknownUsernameAndWrongPassword_ReturnIdenticalGeneric401()
    {
        var user = await CreateUserAsync(
            "login-secret",
            "Login Secret",
            UserRole.Employee,
            "right-pass-1"
        );
        var client = _factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "does-not-exist", password = "anything" }
        );
        var wrongResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "wrong-pass-1" }
        );

        unknownResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        var wrongBody = await wrongResponse.Content.ReadAsStringAsync();
        unknownBody.Should().Be(wrongBody);
        ExtractError(unknownBody).Should().Be("unauthorized");
        ExtractError(wrongBody).Should().Be("unauthorized");
    }

    [Fact]
    public async Task Login_LockedAccount_Returns429WithRetryAfter()
    {
        var user = await CreateUserAsync(
            "login-lock",
            "Login Lock",
            UserRole.Employee,
            "lock-pass-1"
        );
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var fail = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { username = user.Username, password = "wrong" }
            );
            fail.StatusCode.Should()
                .Be(HttpStatusCode.Unauthorized, $"attempt {i + 1} should be 401");
        }

        var locked = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "wrong" }
        );

        locked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        locked.Headers.Should().Contain(h => h.Key == "Retry-After");
        var retryAfter = locked.Headers.GetValues("Retry-After").FirstOrDefault();
        int.Parse(retryAfter!).Should().BeInRange(1, 15 * 60);
        ExtractError(await locked.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_InactiveAccount_ReturnsGeneric401()
    {
        var user = await CreateUserAsync(
            "login-inactive",
            "Login Inactive",
            UserRole.Admin,
            "inactive-pass-1"
        );
        user.IsActive = false;
        await UpdateUserAsync(user);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "inactive-pass-1" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        ExtractError(await response.Content.ReadAsStringAsync()).Should().Be("unauthorized");
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

    private async Task UpdateUserAsync(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await store.UpdateAsync(user);
    }

    private static string ExtractError(string body)
    {
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("error").GetString() ?? string.Empty;
    }
}
