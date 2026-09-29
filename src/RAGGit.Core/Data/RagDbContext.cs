using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace RAGGit.Core.Data;

/// <summary>
/// Lightweight SQLite context for the single-tenant RAG library metadata.
/// Per the MS persistence-layer design this class is the Unit of Work /
/// connection factory plus schema bootstrap only: all Queries/Documents/Users
/// reads and writes go through the aggregate repositories
/// (<c>SqliteQueryRepository</c>, <c>SqliteDocumentRepository</c>,
/// <c>SqliteUserRepository</c>). Do not add query methods here.
/// Stores Documents, Chunks, Queries and the singleton Library row.
/// </summary>
public sealed class RagDbContext : IAsyncDisposable
{
    private readonly string _connectionString;

    public RagDbContext(string connectionString)
    {
        _connectionString =
            connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Creates the schema if it does not exist and seeds the singleton Library row (id=1).
    /// </summary>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var sql in GetSchemaCommands())
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = (SqliteTransaction)transaction;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureDocumentColumnAsync(
            connection,
            transaction,
            "CreatedByName",
            "TEXT",
            cancellationToken
        );

        await EnsureDocumentColumnAsync(
            connection,
            transaction,
            "FailureReason",
            "TEXT",
            cancellationToken
        );

        using var seedCommand = connection.CreateCommand();
        seedCommand.Transaction = (SqliteTransaction)transaction;
        seedCommand.CommandText =
            @"
            INSERT OR IGNORE INTO Library (Id, Name, CreatedAt)
            VALUES (1, 'RAGGit Library', @createdAt);";
        seedCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a nullable <c>Documents</c> column to databases created before the
    /// column existed (<c>CreatedByName</c> arrived with 009, <c>FailureReason</c>
    /// with the background-ingest refactor). SQLite has no ADD COLUMN IF NOT
    /// EXISTS on all supported versions, so probe first and alter only when
    /// absent. Fresh databases already carry the columns via
    /// <see cref="GetSchemaCommands"/>.
    /// </summary>
    private static async Task EnsureDocumentColumnAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string columnName,
        string columnType,
        CancellationToken cancellationToken
    )
    {
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.Transaction = (SqliteTransaction)transaction;
        pragmaCommand.CommandText = "SELECT name FROM pragma_table_info('Documents');";
        await using var reader = await pragmaCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(0), columnName, StringComparison.Ordinal))
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.Transaction = (SqliteTransaction)transaction;
        alterCommand.CommandText = $"ALTER TABLE Documents ADD COLUMN {columnName} {columnType};";
        await alterCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<string> GetSchemaCommands()
    {
        yield return @"
            CREATE TABLE IF NOT EXISTS Library (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );";

        yield return @"
            CREATE TABLE IF NOT EXISTS Documents (
                Id TEXT PRIMARY KEY,
                Filename TEXT NOT NULL,
                Mime TEXT NOT NULL,
                Size INTEGER NOT NULL,
                Hash TEXT UNIQUE NOT NULL,
                Status TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                CreatedByName TEXT,
                FailureReason TEXT,
                CreatedAt TEXT NOT NULL
            );";

        yield return @"
            CREATE TABLE IF NOT EXISTS Chunks (
                Id TEXT PRIMARY KEY,
                DocumentId TEXT NOT NULL REFERENCES Documents(Id) ON DELETE CASCADE,
                Ordinal INTEGER NOT NULL,
                Text TEXT NOT NULL,
                TokenCount INTEGER NOT NULL
            );";

        yield return @"
            CREATE TABLE IF NOT EXISTS Queries (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Prompt TEXT NOT NULL,
                RetrievedChunkIds TEXT,
                Answer TEXT,
                CitationIds TEXT,
                LatencyMs INTEGER,
                CreatedAt TEXT NOT NULL
            );";

        yield return @"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL COLLATE NOCASE,
                DisplayName TEXT NOT NULL,
                Role TEXT NOT NULL CHECK (Role IN ('Admin','Employee')),
                PasswordHash TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                FailedAccessCount INTEGER NOT NULL DEFAULT 0,
                LockoutUntil TEXT,
                MustChangePassword INTEGER NOT NULL DEFAULT 0,
                LastSignInAt TEXT,
                LastPasswordChangedAt TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );";

        yield return @"
            CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Username
            ON Users (Username);";

        yield return @"
            CREATE INDEX IF NOT EXISTS IX_Users_Active
            ON Users (IsActive);";
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
