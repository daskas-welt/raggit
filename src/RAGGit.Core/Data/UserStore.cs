using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Models;

namespace RAGGit.Core.Data;

/// <summary>
/// Reads and writes <see cref="User"/> accounts to the SQLite metadata database.
/// </summary>
public sealed class UserStore
{
    private readonly RagDbContext _db;

    public UserStore(RagDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<User?> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Username, DisplayName, Role, PasswordHash, IsActive,
                   FailedAccessCount, LockoutUntil, MustChangePassword,
                   LastSignInAt, LastPasswordChangedAt, CreatedAt
            FROM Users
            WHERE Username = @username
            COLLATE NOCASE
            LIMIT 1;";
        command.Parameters.AddWithValue("@username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return Map(reader);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Username, DisplayName, Role, PasswordHash, IsActive,
                   FailedAccessCount, LockoutUntil, MustChangePassword,
                   LastSignInAt, LastPasswordChangedAt, CreatedAt
            FROM Users
            WHERE Id = @id
            LIMIT 1;";
        command.Parameters.AddWithValue("@id", id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return Map(reader);
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Username, DisplayName, Role, PasswordHash, IsActive,
                   FailedAccessCount, LockoutUntil, MustChangePassword,
                   LastSignInAt, LastPasswordChangedAt, CreatedAt
            FROM Users
            ORDER BY CreatedAt;";

        var users = new List<User>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(Map(reader));
        }

        return users;
    }

    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            INSERT INTO Users (Id, Username, DisplayName, Role, PasswordHash, IsActive,
                               FailedAccessCount, LockoutUntil, MustChangePassword,
                               LastSignInAt, LastPasswordChangedAt, CreatedAt)
            VALUES (@id, @username, @displayName, @role, @passwordHash, @isActive,
                    @failedAccessCount, @lockoutUntil, @mustChangePassword,
                    @lastSignInAt, @lastPasswordChangedAt, @createdAt);";
        AddParameters(command, user);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException($"Username '{user.Username}' already exists.", ex);
        }
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            UPDATE Users
            SET Username = @username,
                DisplayName = @displayName,
                Role = @role,
                PasswordHash = @passwordHash,
                IsActive = @isActive,
                FailedAccessCount = @failedAccessCount,
                LockoutUntil = @lockoutUntil,
                MustChangePassword = @mustChangePassword,
                LastSignInAt = @lastSignInAt,
                LastPasswordChangedAt = @lastPasswordChangedAt,
                CreatedAt = @createdAt
            WHERE Id = @id;";
        AddParameters(command, user);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(SqliteCommand command, User user)
    {
        command.Parameters.AddWithValue("@id", user.Id.ToString());
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@displayName", user.DisplayName);
        command.Parameters.AddWithValue("@role", user.Role.ToString());
        command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@failedAccessCount", user.FailedAccessCount);
        command.Parameters.AddWithValue(
            "@lockoutUntil",
            user.LockoutUntil.HasValue
                ? (object)user.LockoutUntil.Value.ToString("O")
                : DBNull.Value
        );
        command.Parameters.AddWithValue("@mustChangePassword", user.MustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue(
            "@lastSignInAt",
            user.LastSignInAt.HasValue
                ? (object)user.LastSignInAt.Value.ToString("O")
                : DBNull.Value
        );
        command.Parameters.AddWithValue(
            "@lastPasswordChangedAt",
            user.LastPasswordChangedAt.ToString("O")
        );
        command.Parameters.AddWithValue("@createdAt", user.CreatedAt.ToString("O"));
    }

    private static User Map(SqliteDataReader reader)
    {
        return new User
        {
            Id = Guid.Parse(reader.GetString(0)),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            Role = Enum.Parse<UserRole>(reader.GetString(3)),
            PasswordHash = reader.GetString(4),
            IsActive = reader.GetInt64(5) != 0,
            FailedAccessCount = (int)reader.GetInt64(6),
            LockoutUntil = reader.IsDBNull(7) ? null : ParseUtc(reader.GetString(7)),
            MustChangePassword = reader.GetInt64(8) != 0,
            LastSignInAt = reader.IsDBNull(9) ? null : ParseUtc(reader.GetString(9)),
            LastPasswordChangedAt = ParseUtc(reader.GetString(10)),
            CreatedAt = ParseUtc(reader.GetString(11)),
        };
    }

    private static DateTime ParseUtc(string value) =>
        DateTime
            .Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToUniversalTime();
}
