using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Abstractions;
using RAGGit.Ingest.Vector;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class DimensionGuardTests
{
    private static VectorRecord MakeRecord(int vectorSize, string documentId = "doc-1")
    {
        var vector = new float[vectorSize];
        vector[0] = 1.0f;
        return new VectorRecord(
            Guid.NewGuid(),
            vector,
            new Dictionary<string, object?>
            {
                ["documentId"] = documentId,
                ["text"] = "hello",
                ["ordinal"] = 0,
            }
        );
    }

    [Fact]
    public async Task ValidateDimension_Mismatch_ThrowsWithBothDimensionsAndRecovery()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "raggit-dim-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);
        try
        {
            // Seed at 384
            var client384 = new LanceDbLocalClient(tempPath, vectorSize: 384);
            await client384.UpsertAsync(new[] { MakeRecord(384) });
            client384.Dispose();

            // Reopen at 768 should mismatch
            var client768 = new LanceDbLocalClient(tempPath, vectorSize: 768);
            var act = async () => await client768.ValidateDimensionAsync(768);
            var ex = await Assert.ThrowsAsync<DimensionMismatchException>(act);
            ex.Message.Should().Contain("768");
            ex.Message.Should().Contain("384");
            ex.Message.Should().Contain("delete data/lancedb");
            ex.ConfiguredDimension.Should().Be(768);
            ex.StoredDimension.Should().Be(384);
            client768.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ValidateDimension_MatchingDimension_NoThrow()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "raggit-dim-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);
        try
        {
            var client = new LanceDbLocalClient(tempPath, vectorSize: 384);
            await client.UpsertAsync(new[] { MakeRecord(384) });
            // Same dimension should not throw
            await client.ValidateDimensionAsync(384);
            client.Dispose();

            var client2 = new LanceDbLocalClient(tempPath, vectorSize: 384);
            await client2.ValidateDimensionAsync(384);
            client2.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ValidateDimension_AbsentTable_NoThrow()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "raggit-dim-" + Guid.NewGuid());
        Directory.CreateDirectory(tempPath);
        try
        {
            var client = new LanceDbLocalClient(tempPath, vectorSize: 768);
            // No table created yet — lazy-create allowed, no exception
            await client.ValidateDimensionAsync(768);
            client.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(tempPath, recursive: true);
            }
            catch { }
        }
    }
}
