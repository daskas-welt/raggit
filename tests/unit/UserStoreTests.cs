using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T007: Verify UserStore lookup, create, and update operations.
/// </summary>
public sealed class UserStoreTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;
    private readonly UserStore _store;

    public UserStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-t007-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
        _store = new UserStore(_db);
    }

    public async Task InitializeAsync() => await _db.EnsureCreatedAsync();

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public async Task CreateAsync_ThenGetByUsername_ReturnsUser_CaseInsensitive()
    {
        var user = NewUser("ada", "Ada Lovelace", UserRole.Admin);

        await _store.CreateAsync(user);
        var found = await _store.GetByUsernameAsync("ADA");

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
        found.Username.Should().Be("ada");
        found.DisplayName.Should().Be("Ada Lovelace");
        found.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task GetByUsername_Unknown_ReturnsNull()
    {
        var found = await _store.GetByUsernameAsync("nobody");
        found.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_Throws()
    {
        var user = NewUser("bob", "Bob", UserRole.Employee);
        await _store.CreateAsync(user);

        var duplicate = NewUser("BOB", "Another Bob", UserRole.Employee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.CreateAsync(duplicate));
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var user = NewUser("carol", "Carol", UserRole.Employee);
        await _store.CreateAsync(user);

        user.IsActive = false;
        user.FailedAccessCount = 3;
        user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
        await _store.UpdateAsync(user);

        var found = await _store.GetByIdAsync(user.Id);
        found.Should().NotBeNull();
        found!.IsActive.Should().BeFalse();
        found.FailedAccessCount.Should().Be(3);
        found
            .LockoutUntil.Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    private static User NewUser(string username, string displayName, UserRole role)
    {
        var now = DateTime.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword("password123"),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
    }
}
