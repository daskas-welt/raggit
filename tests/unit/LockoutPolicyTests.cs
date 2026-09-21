using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T019: Account lockout policy — 5 failures within a sliding window locks for 15 minutes.
/// </summary>
public sealed class LockoutPolicyTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;
    private readonly IUserRepository _store;
    private readonly LockoutPolicyOptions _options;
    private readonly LockoutPolicy _policy;

    public LockoutPolicyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-t019-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
        _store = new SqliteUserRepository(_db);
        _options = new LockoutPolicyOptions { Threshold = 5, Minutes = 15 };
        _policy = new LockoutPolicy(_store, Options.Create(_options));
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
    public async Task RecordFailure_BelowThreshold_DoesNotLock()
    {
        var user = await CreateUserAsync("fail-4");

        for (var i = 0; i < 4; i++)
        {
            await _policy.RecordFailureAsync(user);
        }

        user.FailedAccessCount.Should().Be(4);
        user.IsLockedOut.Should().BeFalse();
        var persisted = await _store.GetByIdAsync(user.Id);
        persisted!.FailedAccessCount.Should().Be(4);
        persisted.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task RecordFailure_AtThreshold_LocksForConfiguredMinutes()
    {
        var user = await CreateUserAsync("fail-5");

        for (var i = 0; i < 5; i++)
        {
            await _policy.RecordFailureAsync(user);
        }

        user.FailedAccessCount.Should().Be(5);
        user.IsLockedOut.Should().BeTrue();
        user.LockoutUntil.Should().NotBeNull();
        user.LockoutUntil!.Value.Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(_options.Minutes), TimeSpan.FromSeconds(5));

        var persisted = await _store.GetByIdAsync(user.Id);
        persisted!.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task RecordSuccess_ResetsFailureCountAndClearsLockout()
    {
        var user = await CreateUserAsync("success");
        for (var i = 0; i < 5; i++)
        {
            await _policy.RecordFailureAsync(user);
        }

        await _policy.RecordSuccessAsync(user);

        user.FailedAccessCount.Should().Be(0);
        user.LockoutUntil.Should().BeNull();
        user.IsLockedOut.Should().BeFalse();

        var persisted = await _store.GetByIdAsync(user.Id);
        persisted!.FailedAccessCount.Should().Be(0);
        persisted.LockoutUntil.Should().BeNull();
    }

    private async Task<User> CreateUserAsync(string username)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = UserRole.Employee,
            PasswordHash = PasswordHasher.HashPassword("any-password"),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
        await _store.CreateAsync(user);
        return user;
    }
}
