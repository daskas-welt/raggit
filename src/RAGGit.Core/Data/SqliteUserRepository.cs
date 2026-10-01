using System;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Core.Data;

/// <summary>
/// SQLite implementation of <see cref="IUserRepository"/> — the single
/// persistence channel for the <see cref="User"/> aggregate root, per the
/// MS persistence-layer design (one repository per aggregate root).
/// </summary>
public class SqliteUserRepository : IUserRepository
{
    private readonly RagDbContext _db;

    public SqliteUserRepository(RagDbContext db)
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
                PasswordHash = @passwordHash,
                FailedAccessCount = @failedAccessCount,
                LockoutUntil = @lockoutUntil,
                MustChangePassword = @mustChangePassword,
                LastSignInAt = @lastSignInAt,
                LastPasswordChangedAt = @lastPasswordChangedAt,
                CreatedAt = @createdAt
            WHERE Id = @id;";
        // Role and IsActive are intentionally excluded. All access-state writes go through
        // TryPatchAsync, which evaluates the last-active-admin invariant in the write transaction.
        AddParameters(command, user, includeAccessState: false);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<UserPatchResult> TryPatchAsync(
        Guid id,
        UserRole? role,
        bool? isActive,
        string? displayName,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // Acquire SQLite's write reservation before reading the target/admin count. This makes the
        // invariant check and patch serial with other writers, including other API processes.
        using var transaction = connection.BeginTransaction(
            IsolationLevel.Serializable,
            deferred: false
        );

        User? current;
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText =
                @"
                SELECT Id, Username, DisplayName, Role, PasswordHash, IsActive,
                       FailedAccessCount, LockoutUntil, MustChangePassword,
                       LastSignInAt, LastPasswordChangedAt, CreatedAt
                FROM Users
                WHERE Id = @id
                LIMIT 1;";
            read.Parameters.AddWithValue("@id", id.ToString());

            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            current = await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }

        if (current is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UserPatchResult.NotFound();
        }

        var nextRole = role ?? current.Role;
        var nextIsActive = isActive ?? current.IsActive;
        var removesActiveAdmin =
            current.Role == UserRole.Admin
            && current.IsActive
            && (nextRole != UserRole.Admin || !nextIsActive);

        if (removesActiveAdmin)
        {
            using var count = connection.CreateCommand();
            count.Transaction = transaction;
            count.CommandText =
                "SELECT COUNT(*) FROM Users WHERE Role = 'Admin' AND IsActive = 1 AND Id <> @id;";
            count.Parameters.AddWithValue("@id", id.ToString());
            var otherActiveAdmins = Convert.ToInt64(
                await count.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture
            );
            if (otherActiveAdmins == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return UserPatchResult.LastActiveAdmin();
            }
        }

        current.Role = nextRole;
        current.IsActive = nextIsActive;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            current.DisplayName = displayName.Trim();
        }
        if (isActive == false)
        {
            // Preserve existing behavior: deactivation clears a temporary lockout.
            current.LockoutUntil = null;
            current.FailedAccessCount = 0;
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                @"
                UPDATE Users
                SET DisplayName = @displayName,
                    Role = @role,
                    IsActive = @isActive,
                    FailedAccessCount = @failedAccessCount,
                    LockoutUntil = @lockoutUntil
                WHERE Id = @id;";
            update.Parameters.AddWithValue("@id", id.ToString());
            update.Parameters.AddWithValue("@displayName", current.DisplayName);
            update.Parameters.AddWithValue("@role", current.Role.ToString());
            update.Parameters.AddWithValue("@isActive", current.IsActive ? 1 : 0);
            update.Parameters.AddWithValue("@failedAccessCount", current.FailedAccessCount);
            update.Parameters.AddWithValue(
                "@lockoutUntil",
                current.LockoutUntil.HasValue
                    ? (object)current.LockoutUntil.Value.ToString("O")
                    : DBNull.Value
            );
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return UserPatchResult.Updated(current);
    }

    private static void AddParameters(
        SqliteCommand command,
        User user,
        bool includeAccessState = true
    )
    {
        command.Parameters.AddWithValue("@id", user.Id.ToString());
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@displayName", user.DisplayName);
        if (includeAccessState)
        {
            command.Parameters.AddWithValue("@role", user.Role.ToString());
            command.Parameters.AddWithValue("@isActive", user.IsActive ? 1 : 0);
        }
        command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
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
