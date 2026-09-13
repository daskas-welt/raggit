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

namespace RAGGit.Tests.Contract;

/// <summary>
/// T024: Contract tests for GET|POST /api/users per contracts/api.yaml 1.3.0.
/// </summary>
public sealed class UsersCrudContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public UsersCrudContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUsers_Admin_ReturnsListWithoutPasswordHash()
    {
        await CreateUserAsync("admin-list", "Admin List", UserRole.Admin, "admin-list-pass-1");
        await CreateUserAsync(
            "employee-list",
            "Employee List",
            UserRole.Employee,
            "employee-list-pass-1"
        );

        var adminClient = CreateAdminClient();
        var response = await adminClient.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("passwordHash");
        body.Should().NotContain("PasswordHash");

        var doc = JsonDocument.Parse(body);
        var array = doc.RootElement.EnumerateArray().ToList();
        array.Should().Contain(u => u.GetProperty("username").GetString() == "admin-list");
        array.Should().Contain(u => u.GetProperty("username").GetString() == "employee-list");
        array
            .Should()
            .OnlyContain(u =>
                !u.ToString().Contains("passwordHash", StringComparison.OrdinalIgnoreCase)
            );
    }

    [Fact]
    public async Task PostUser_Admin_Returns201WithoutPasswordHash()
    {
        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync(
            "/api/users",
            new
            {
                username = "created-admin",
                displayName = "Created Admin",
                role = "Admin",
                password = "new-admin-pass-1",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("passwordHash");
        body.Should().NotContain("PasswordHash");

        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        doc.RootElement.GetProperty("username").GetString().Should().Be("created-admin");
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Created Admin");
        doc.RootElement.GetProperty("role").GetString().Should().Be("Admin");
        doc.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var loginResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "created-admin", password = "new-admin-pass-1" }
        );
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostUser_DuplicateUsername_Returns409()
    {
        await CreateUserAsync("dup-user", "Duplicate User", UserRole.Employee, "dup-pass-1");

        var adminClient = CreateAdminClient();
        var response = await adminClient.PostAsJsonAsync(
            "/api/users",
            new
            {
                username = "DUP-USER",
                displayName = "Duplicate User Case",
                role = "Employee",
                password = "dup-pass-2",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync();
        ExtractError(body).Should().Contain("already exists");
    }

    [Fact]
    public async Task GetUsers_Employee_Returns403()
    {
        var employeeClient = CreateEmployeeClient();
        var response = await employeeClient.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostUser_Employee_Returns403()
    {
        var employeeClient = CreateEmployeeClient();
        var response = await employeeClient.PostAsJsonAsync(
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

    private static string ExtractError(string body)
    {
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("error").GetString() ?? string.Empty;
    }
}
