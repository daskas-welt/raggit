using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace RAGGit.Core.Data;

/// <summary>
/// Lightweight SQLite context for the single-tenant RAG library metadata.
/// Stores Documents, Chunks, Queries and the singleton Library row.
/// </summary>
public sealed class RagDbContext : IAsyncDisposable
{
    private readonly string _connectionString;

    public RagDbContext(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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

        using var seedCommand = connection.CreateCommand();
        seedCommand.Transaction = (SqliteTransaction)transaction;
        seedCommand.CommandText = @"
            INSERT OR IGNORE INTO Library (Id, Name, CreatedAt)
            VALUES (1, 'RAGGit Library', @createdAt);";
        seedCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        await seedCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
