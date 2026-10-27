using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Data;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// The Queries table no longer carries the removed query-mode audit column.
/// </summary>
public sealed class RagDbContextQueriesSchemaTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;

    public RagDbContextQueriesSchemaTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-queries-{Guid.NewGuid()}.db");
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
    public async Task QueriesTable_HasNoModeColumn()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Queries');";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        columns.Should().NotContain("Mode");
    }

    [Fact]
    public async Task ExistingDatabaseWithModeColumn_StillAcceptsQueries()
    {
        var legacyDbPath = Path.Combine(Path.GetTempPath(), $"raggit-legacy-{Guid.NewGuid()}.db");
        await using (var legacy = new SqliteConnection($"Data Source={legacyDbPath}"))
        {
            await legacy.OpenAsync();
            using var create = legacy.CreateCommand();
            create.CommandText =
                @"
            CREATE TABLE Queries (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Prompt TEXT NOT NULL,
                RetrievedChunkIds TEXT,
                Answer TEXT,
                CitationIds TEXT,
                LatencyMs INTEGER,
                CreatedAt TEXT NOT NULL,
                Mode TEXT
            );";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var legacyDb = new RagDbContext($"Data Source={legacyDbPath}");
            await legacyDb.EnsureCreatedAsync();
            var repository = new SqliteQueryRepository(legacyDb);
            var query = new RAGGit.Core.Models.Query
            {
                Id = Guid.NewGuid(),
                UserId = "legacy-user",
                Prompt = "still works?",
                CreatedAt = DateTime.UtcNow,
            };

            await repository.AddAsync(query);

            var page = await repository.ListHistoryAsync("legacy-user", 10, 0);
            page.Items.Should().ContainSingle().Which.PromptPreview.Should().Be("still works?");
            await legacyDb.DisposeAsync();
        }
        finally
        {
            try
            {
                File.Delete(legacyDbPath);
            }
            catch
            {
                // best effort
            }
        }
    }
}
