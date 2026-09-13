using System.Net;
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
/// T027: Employee callers are refused Admin-only people-management actions.
/// </summary>
public sealed class AdminRbacTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AdminRbacTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Employee_GetUsers_Returns403()
    {
        var client = CreateEmployeeClient();
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_PostUser_Returns403()
    {
        var client = CreateEmployeeClient();
        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                username = "employee-create",
                displayName = "Employee Create",
                role = "Employee",
                password = "employee-create-pass-1",
            }
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_PatchUser_Returns403()
    {
        var user = await CreateUserAsync(
            "employee-patch-target",
            "Target",
            UserRole.Employee,
            "target-pass-1"
        );
        var client = CreateEmployeeClient();
        var response = await client.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { isActive = false }
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_ResetPassword_Returns403()
    {
        var user = await CreateUserAsync(
            "employee-reset-target",
            "Target",
            UserRole.Employee,
            "target-pass-2"
        );
        var client = CreateEmployeeClient();
        var response = await client.PostAsJsonAsync(
            $"/api/users/{user.Id}/reset-password",
            new { password = "new-pass-1" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminBearer_GetUsers_Returns200()
    {
        await CreateUserAsync(
            "admin-bearer-user",
            "Admin Bearer User",
            UserRole.Admin,
            "admin-bearer-pass-1"
        );
        var token = await LoginAsync("admin-bearer-user", "admin-bearer-pass-1");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<User> CreateUserAsync(
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

    private HttpClient CreateEmployeeClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        return client;
    }
}
