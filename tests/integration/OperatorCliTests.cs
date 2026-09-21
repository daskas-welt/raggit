using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api.Cli;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T010: Operator CLI `user add` provisions accounts directly into the Users table.
/// </summary>
public sealed class OperatorCliTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;
    private readonly IUserRepository _store;
    private readonly OperatorCli _cli;

    public OperatorCliTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-t010-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
        _store = new SqliteUserRepository(_db);
        _cli = new OperatorCli();
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
    public async Task UserAdd_CreatesActiveUser_WithHashedPassword()
    {
        var exit = await _cli.RunAsync(
            new[]
            {
                "user",
                "add",
                "--username",
                "ada",
                "--display-name",
                "Ada Lovelace",
                "--role",
                "Admin",
                "--password",
                "super-secret-1",
            },
            _store
        );

        exit.Should().Be(0);

        var user = await _store.GetByUsernameAsync("ada");
        user.Should().NotBeNull();
        user!.Username.Should().Be("ada");
        user.DisplayName.Should().Be("Ada Lovelace");
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
        user.PasswordHash.Should().StartWith("PBKDF2-SHA256$310000$");
        PasswordHasher.VerifyPassword("super-secret-1", user.PasswordHash).Should().BeTrue();
        PasswordHasher.VerifyPassword("wrong-password", user.PasswordHash).Should().BeFalse();
    }

    [Fact]
    public async Task UserAdd_DuplicateUsernameCaseInsensitive_ReturnsExit2()
    {
        var first = await _cli.RunAsync(
            new[]
            {
                "user",
                "add",
                "--username",
                "bob",
                "--display-name",
                "Bob Moore",
                "--role",
                "Employee",
                "--password",
                "secret-bob",
            },
            _store
        );
        first.Should().Be(0);

        var duplicate = await _cli.RunAsync(
            new[]
            {
                "user",
                "add",
                "--username",
                "BOB",
                "--display-name",
                "Another Bob",
                "--role",
                "Employee",
                "--password",
                "secret-bob-2",
            },
            _store
        );

        duplicate.Should().Be(2);
    }

    [Fact]
    public async Task UserAdd_InvalidRole_ReturnsExit3()
    {
        var exit = await _cli.RunAsync(
            new[]
            {
                "user",
                "add",
                "--username",
                "carol",
                "--display-name",
                "Carol",
                "--role",
                "Manager",
                "--password",
                "secret-carol",
            },
            _store
        );

        exit.Should().Be(3);

        var user = await _store.GetByUsernameAsync("carol");
        user.Should().BeNull();
    }
}
