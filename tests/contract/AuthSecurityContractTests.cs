using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T040: Security hardening contract tests — HTTPS-only auth, weak-password rejection,
/// no PasswordHash serialization, and tampered-token refusal.
/// </summary>
public sealed class AuthSecurityContractTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Login_OverHttp_InProduction_Returns403HttpsRequired()
    {
        using var factory = new SecurityTestApiFactory();
        var user = await CreateUserAsync(factory, "http-user", "HTTP User", UserRole.Admin, "strong-pass-1");
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = user.Username, password = "strong-pass-1" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        ExtractError(body).Should().Contain("HTTPS");
    }

    [Fact]
    public async Task CreateUser_WeakPassword_Returns400()
    {
        using var factory = new SecurityTestApiFactory();
        var admin = await CreateUserAsync(factory, "admin-weak", "Admin", UserRole.Admin, "strong-pass-1");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IssueToken(factory, admin)
        );

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new { username = "new-user", displayName = "New", role = "Employee", password = "short" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ExtractError(await response.Content.ReadAsStringAsync()).Should().Contain("password");
    }

    [Fact]
    public async Task ListUsers_Response_DoesNotContainPasswordHash()
    {
        using var factory = new SecurityTestApiFactory();
        var admin = await CreateUserAsync(factory, "admin-list", "Admin", UserRole.Admin, "strong-pass-1");
        await CreateUserAsync(factory, "employee-list", "Employee", UserRole.Employee, "strong-pass-2");
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            IssueToken(factory, admin)
        );

        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("passwordHash");
        body.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task TamperedToken_MissingSignature_Returns401()
    {
        using var factory = new SecurityTestApiFactory();
        var user = await CreateUserAsync(factory, "tamper-user", "Tamper", UserRole.Employee, "strong-pass-1");
        var token = IssueToken(factory, user);
        var tampered = token[..token.LastIndexOf('.')] + ".ZmFrZXNpZ25hdHVyZQ";

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string IssueToken(SecurityTestApiFactory factory, User user)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        return tokenService.IssueToken(user);
    }

    private static async Task<User> CreateUserAsync(
        SecurityTestApiFactory factory,
        string username,
        string displayName,
        UserRole role,
        string password
    )
    {
        using var scope = factory.Services.CreateScope();
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
        return user;
    }

    private static string ExtractError(string body)
    {
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("error").GetString() ?? string.Empty;
    }

    /// <summary>
    /// Runs with Production environment so the HTTPS-only auth enforcement is active.
    /// </summary>
    private sealed class SecurityTestApiFactory : TestApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
        }
    }
}
