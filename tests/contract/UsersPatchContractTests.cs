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
/// T025: Contract tests for PATCH /api/users/{id} and POST /api/users/{id}/reset-password.
/// </summary>
public sealed class UsersPatchContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public UsersPatchContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Patch_Deactivate_Admin_Returns200AndNextBearerRequestIs401()
    {
        var user = await CreateUserAsync(
            "deactivate-me",
            "Deactivate Me",
            UserRole.Employee,
            "deactivate-pass-1"
        );
        var token = await LoginAsync("deactivate-me", "deactivate-pass-1");

        var adminClient = CreateAdminClient();
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { isActive = false }
        );

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        patched.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var bearerClient = _factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );
        var next = await bearerClient.GetAsync("/api/auth/me");
        next.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_RoleChange_EmployeeToAdmin_TakesEffectImmediately()
    {
        var user = await CreateUserAsync(
            "promote-me",
            "Promote Me",
            UserRole.Employee,
            "promote-pass-1"
        );
        var token = await LoginAsync("promote-me", "promote-pass-1");

        var adminClient = CreateAdminClient();
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { role = "Admin" }
        );

        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var bearerClient = _factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );
        var me = await bearerClient.GetAsync("/api/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task Patch_DisplayName_ReturnsUpdatedName()
    {
        var user = await CreateUserAsync(
            "rename-me",
            "Rename Me",
            UserRole.Employee,
            "rename-pass-1"
        );

        var adminClient = CreateAdminClient();
        var patch = await adminClient.PatchAsJsonAsync(
            $"/api/users/{user.Id}",
            new { displayName = "Renamed User" }
        );

        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await patch.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Renamed User");
    }

    [Fact]
    public async Task Patch_DemoteLastActiveAdmin_Returns409AndLeavesAccountUnchanged()
    {
        await DemoteExistingAdminsAsync();
        var admin = await CreateUserAsync(
            "last-admin-demote",
            "Last Admin Demote",
            UserRole.Admin,
            "last-admin-pass-1"
        );
        var client = CreateAdminClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/users/{admin.Id}",
            new { role = "Employee" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        error.RootElement.GetProperty("error").GetString().Should().Contain("last active Admin");

        var stored = await client.GetAsync($"/api/users/{admin.Id}");
        stored.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = JsonDocument.Parse(await stored.Content.ReadAsStringAsync());
        account.RootElement.GetProperty("role").GetString().Should().Be("Admin");
        account.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Patch_DeactivateLastActiveAdmin_Returns409AndLeavesAccountUnchanged()
    {
        await DemoteExistingAdminsAsync();
        var admin = await CreateUserAsync(
            "last-admin-disable",
            "Last Admin Disable",
            UserRole.Admin,
            "last-admin-pass-2"
        );
        var client = CreateAdminClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/users/{admin.Id}",
            new { isActive = false }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        error.RootElement.GetProperty("error").GetString().Should().Contain("last active Admin");

        var stored = await client.GetAsync($"/api/users/{admin.Id}");
        stored.StatusCode.Should().Be(HttpStatusCode.OK);
        var account = JsonDocument.Parse(await stored.Content.ReadAsStringAsync());
        account.RootElement.GetProperty("role").GetString().Should().Be("Admin");
        account.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Patch_RemoveOneOfTwoActiveAdmins_IsAllowed()
    {
        await DemoteExistingAdminsAsync();
        var first = await CreateUserAsync(
            "one-of-two-admins",
            "First Admin",
            UserRole.Admin,
            "first-admin-pass-1"
        );
        await CreateUserAsync(
            "second-of-two-admins",
            "Second Admin",
            UserRole.Admin,
            "second-admin-pass-1"
        );
        var client = CreateAdminClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/users/{first.Id}",
            new { role = "Employee" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var remaining = await client.GetAsync("/api/users");
        var accounts = JsonDocument.Parse(await remaining.Content.ReadAsStringAsync());
        accounts
            .RootElement.EnumerateArray()
            .Count(account =>
                account.GetProperty("role").GetString() == "Admin"
                && account.GetProperty("isActive").GetBoolean()
            )
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task ResetPassword_Admin_Returns204AndMustChangePasswordFlag()
    {
        var user = await CreateUserAsync(
            "reset-me",
            "Reset Me",
            UserRole.Employee,
            "reset-old-pass-1"
        );

        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync(
            $"/api/users/{user.Id}/reset-password",
            new { password = "reset-new-pass-1", mustChangePassword = true }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await adminClient.GetAsync($"/api/users/{user.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();

        var login = await _factory
            .CreateClient()
            .PostAsJsonAsync(
                "/api/auth/login",
                new { username = "reset-me", password = "reset-new-pass-1" }
            );
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_Employee_Returns403()
    {
        var user = await CreateUserAsync(
            "reset-employee-target",
            "Target",
            UserRole.Employee,
            "target-pass-1"
        );

        var employeeClient = CreateEmployeeClient();
        var response = await employeeClient.PostAsJsonAsync(
            $"/api/users/{user.Id}/reset-password",
            new { password = "reset-new-pass-1" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private async Task DemoteExistingAdminsAsync()
    {
        await _factory.ClearUsersAsync();
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);
        return client;
    }

    private HttpClient CreateEmployeeClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        return client;
    }
}
