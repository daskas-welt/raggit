using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// The final-active-admin rule must remain true under overlapping PATCH requests.
/// </summary>
public sealed class LastActiveAdminGuardTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public LastActiveAdminGuardTests(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task ConcurrentDemotions_CannotRemoveEveryActiveAdmin()
    {
        var first = await CreateAdminAsync("concurrent-admin-one");
        var second = await CreateAdminAsync("concurrent-admin-two");
        using var client = CreateAdminClient();

        var responses = await Task.WhenAll(
            client.PatchAsJsonAsync($"/api/users/{first.Id}", new { role = "Employee" }),
            client.PatchAsJsonAsync($"/api/users/{second.Id}", new { isActive = false })
        );

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var activeAdmins = (await users.ListAsync()).Count(user =>
            user.Role == UserRole.Admin && user.IsActive
        );
        activeAdmins.Should().Be(1);
    }

    private async Task<User> CreateAdminAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = UserRole.Admin,
            PasswordHash = PasswordHasher.HashPassword("admin-password-026"),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
        await users.CreateAsync(user);
        return user;
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);
        return client;
    }
}
