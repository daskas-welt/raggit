using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T009: Two-level chunk persistence through
/// <see cref="SqliteDocumentRepository"/> — <c>Level</c>/<c>ParentId</c>
/// round-trip, idempotent schema probe-add on an existing database, and
/// parent/child rows surviving the delete-cascade.
/// </summary>
public sealed class ChunkPersistenceTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;
    private readonly SqliteDocumentRepository _repository;

    public ChunkPersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-t009-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
        _repository = new SqliteDocumentRepository(_db);
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
    public async Task AddChunks_RoundTrips_LevelAndParentId()
    {
        var document = await AddDocumentAsync(DocumentStatus.Ready);
        var parentId = Guid.NewGuid();
        var parent = new Chunk
        {
            Id = parentId,
            DocumentId = document.Id,
            Ordinal = 0,
            Text = "parent passage",
            TokenCount = 2,
            Level = ChunkLevel.Parent,
            ParentId = null,
        };
        var child = new Chunk
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Ordinal = 0,
            Text = "child passage",
            TokenCount = 2,
            Level = ChunkLevel.Child,
            ParentId = parentId,
        };

        await _repository.AddChunksAsync(new[] { parent, child });

        var loaded = await _repository.GetChunksByIdsAsync(new[] { parent.Id, child.Id });
        loaded.Should().HaveCount(2);

        var loadedParent = loaded.Single(c => c.Id == parent.Id);
        loadedParent.Level.Should().Be(ChunkLevel.Parent);
        loadedParent.ParentId.Should().BeNull();

        var loadedChild = loaded.Single(c => c.Id == child.Id);
        loadedChild.Level.Should().Be(ChunkLevel.Child);
        loadedChild.ParentId.Should().Be(parentId);
    }

    [Fact]
    public async Task EnsureCreated_IsIdempotent_OnExistingDatabase()
    {
        var document = await AddDocumentAsync(DocumentStatus.Ready);
        await _repository.AddChunksAsync(
            new[]
            {
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Ordinal = 0,
                    Text = "kept",
                    TokenCount = 1,
                    Level = ChunkLevel.Parent,
                },
            }
        );

        // Second bootstrap over the same file must probe-add nothing and
        // keep every row.
        await _db.EnsureCreatedAsync();
        await _db.EnsureCreatedAsync();

        var loaded = await _repository.GetChunksByIdsAsync((await ListChunkIdsAsync()).ToList());
        loaded.Should().HaveCount(1);
        loaded[0].Text.Should().Be("kept");
    }

    [Fact]
    public async Task Delete_RemovesParentAndChildRows()
    {
        var document = await AddDocumentAsync(DocumentStatus.Ready);
        var parentId = Guid.NewGuid();
        await _repository.AddChunksAsync(
            new[]
            {
                new Chunk
                {
                    Id = parentId,
                    DocumentId = document.Id,
                    Ordinal = 0,
                    Text = "parent",
                    TokenCount = 1,
                    Level = ChunkLevel.Parent,
                },
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Ordinal = 0,
                    Text = "child",
                    TokenCount = 1,
                    Level = ChunkLevel.Child,
                    ParentId = parentId,
                },
            }
        );

        (await _repository.DeleteAsync(document.Id)).Should().BeTrue();

        var remaining = await ListChunkIdsAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task ListReadyDocumentIdsWithoutParentChunk_FindsOnlyStaleReadyDocs()
    {
        var stale = await AddDocumentAsync(DocumentStatus.Ready);
        await _repository.AddChunksAsync(
            new[]
            {
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = stale.Id,
                    Ordinal = 0,
                    Text = "legacy child",
                    TokenCount = 2,
                    Level = ChunkLevel.Child,
                },
            }
        );

        var current = await AddDocumentAsync(DocumentStatus.Ready);
        var currentParent = Guid.NewGuid();
        await _repository.AddChunksAsync(
            new[]
            {
                new Chunk
                {
                    Id = currentParent,
                    DocumentId = current.Id,
                    Ordinal = 0,
                    Text = "parent",
                    TokenCount = 1,
                    Level = ChunkLevel.Parent,
                },
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = current.Id,
                    Ordinal = 0,
                    Text = "child",
                    TokenCount = 1,
                    Level = ChunkLevel.Child,
                    ParentId = currentParent,
                },
            }
        );

        var indexing = await AddDocumentAsync(DocumentStatus.Indexing);

        // A Ready document that produced zero chunks (e.g. an image-only PDF)
        // must NOT be treated as stale, or the backfill would re-queue it on
        // every startup.
        await AddDocumentAsync(DocumentStatus.Ready);

        var staleIds = await _repository.ListReadyDocumentIdsWithoutParentChunkAsync();
        staleIds.Should().Equal(stale.Id);
    }

    private async Task<Document> AddDocumentAsync(DocumentStatus status)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Filename = $"doc-{Guid.NewGuid():N}.txt",
            Mime = DocumentMimeType.Txt,
            Size = 12,
            Hash = Guid.NewGuid().ToString("N"),
            Status = status,
            CreatedBy = "test-user",
        };
        await _repository.AddAsync(document);
        return document;
    }

    private async Task<IReadOnlyList<Guid>> ListChunkIdsAsync()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Chunks;";
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }

        return ids;
    }
}
