using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T006: Verify the Users table DDL is created by RagDbContext.EnsureCreatedAsync.
/// </summary>
public sealed class RagDbContextUsersSchemaTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;

    public RagDbContextUsersSchemaTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-t006-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
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
    public async Task UsersTable_IsCreated()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Users';";
        var tableName = await command.ExecuteScalarAsync();
        tableName.Should().Be("Users");
    }

    [Fact]
    public async Task UsersTable_SupportsInsertAndActiveIndex()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("O");

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        using var insert = connection.CreateCommand();
        insert.CommandText =
            @"
            INSERT INTO Users (Id, Username, DisplayName, Role, PasswordHash, IsActive,
                               FailedAccessCount, LockoutUntil, MustChangePassword,
                               LastSignInAt, LastPasswordChangedAt, CreatedAt)
            VALUES (@id, @username, @displayName, @role, @hash, 1, 0, NULL, 0, NULL, @now, @now);";
        insert.Parameters.AddWithValue("@id", id.ToString());
        insert.Parameters.AddWithValue("@username", "ada");
        insert.Parameters.AddWithValue("@displayName", "Ada Lovelace");
        insert.Parameters.AddWithValue("@role", "Admin");
        insert.Parameters.AddWithValue("@hash", "PBKDF2-SHA256$310000$test");
        insert.Parameters.AddWithValue("@now", now);
        await insert.ExecuteNonQueryAsync();

        using var index = connection.CreateCommand();
        index.CommandText =
            "SELECT name FROM sqlite_master WHERE type='index' AND name='IX_Users_Active';";
        var indexName = await index.ExecuteScalarAsync();
        indexName.Should().Be("IX_Users_Active");
    }
}
