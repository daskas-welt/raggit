using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Abstractions;
using RAGGit.Ingest.Vector;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// LanceDB maintenance: compaction is safe and the vector index is only built
/// once the table clears the row threshold (LanceDB cannot index an empty table).
/// </summary>
public sealed class LanceDbMaintenanceTests
{
    [Fact]
    public async Task OptimizeAsync_OnPopulatedTable_DoesNotThrowAndSearchStillWorks()
    {
        var tempPath = NewTempPath();
        try
        {
            using var client = new LanceDbLocalClient(tempPath, vectorSize: 384);
            await client.UpsertAsync(Enumerable.Range(0, 20).Select(i => Record(384, "doc-1", i)));
            await client.UpsertAsync(Enumerable.Range(0, 20).Select(i => Record(384, "doc-2", i)));

            var stats = await client.OptimizeAsync(TimeSpan.FromDays(7));

            stats.Compaction.Should().NotBeNull();
            (stats.Compaction!.FilesRemoved + stats.Compaction.FilesAdded)
                .Should()
                .BeGreaterThanOrEqualTo(0);

            var query = new float[384];
            query[0] = 1f;
            var hits = await client.SearchAsync(query, limit: 3);
            hits.Should().NotBeEmpty();
        }
        finally
        {
            Cleanup(tempPath);
        }
    }

    [Fact]
    public async Task OptimizeAsync_OnEmptyTable_DoesNotThrow()
    {
        // A batch can fail before any row is written; maintenance then compacts
        // an empty (lazily-created) table. It must be a no-op, not a throw.
        var tempPath = NewTempPath();
        try
        {
            using var client = new LanceDbLocalClient(tempPath, vectorSize: 384);

            var act = async () => await client.OptimizeAsync(TimeSpan.FromDays(7));

            await act.Should().NotThrowAsync();
        }
        finally
        {
            Cleanup(tempPath);
        }
    }

    [Fact]
    public async Task EnsureVectorIndexAsync_SkipsBelowThreshold_ThenBuildsIdempotently()
    {
        var tempPath = NewTempPath();
        try
        {
            using var client = new LanceDbLocalClient(tempPath, vectorSize: 384);
            await client.UpsertAsync(
                Enumerable.Range(0, 1000).Select(i => Record(384, "doc-1", i))
            );

            (await client.EnsureVectorIndexAsync(thresholdRows: 10_000))
                .Should()
                .BeFalse("below the row threshold the index is skipped");

            (await client.EnsureVectorIndexAsync(thresholdRows: 100))
                .Should()
                .BeTrue("at or above the threshold the index is built");

            (await client.EnsureVectorIndexAsync(thresholdRows: 100))
                .Should()
                .BeTrue("an existing index is detected and not rebuilt");
        }
        finally
        {
            Cleanup(tempPath);
        }
    }

    private static VectorRecord Record(int vectorSize, string documentId, int ordinal)
    {
        var vector = new float[vectorSize];
        vector[ordinal % vectorSize] = 1.0f;
        return new VectorRecord(
            Guid.NewGuid(),
            vector,
            new Dictionary<string, object?>
            {
                ["documentId"] = documentId,
                ["text"] = $"chunk {ordinal}",
                ["ordinal"] = ordinal,
            }
        );
    }

    private static string NewTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "raggit-maint-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort; a locked native handle must not fail the test.
        }
    }
}
