using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="Chunker"/>: token-based splitting and SHA-256 hash dedupe.
/// </summary>
public sealed class ChunkingTests
{
    [Fact]
    public async Task ChunkText_Splits_512_Tokens_With_50_Overlap()
    {
        const int tokenCount = 2000;
        const int chunkSize = 512;
        const int overlap = 50;
        const int step = chunkSize - overlap;
        var text = GenerateTokens(tokenCount);
        var documentId = Guid.NewGuid();

        var chunks = Chunker.ChunkText(text, documentId, chunkSize, overlap);

        var expectedChunks = (int)Math.Max(1, Math.Ceiling((tokenCount - overlap) / (double)step));
        chunks.Count.Should().Be(expectedChunks);

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            chunk.DocumentId.Should().Be(documentId);
            chunk.Id.Should().NotBe(Guid.Empty);
            chunk.Ordinal.Should().Be(i);
            chunk.Text.Should().NotBeNullOrWhiteSpace();
            chunk.TokenCount.Should().BeGreaterThan(0);
            chunk.TokenCount.Should().BeLessOrEqualTo(chunkSize);
        }

        for (var i = 0; i < chunks.Count - 1; i++)
        {
            var currentTokens = chunks[i]
                .Text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            var nextTokens = chunks[i + 1]
                .Text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            var actualOverlap = CountOverlap(currentTokens, nextTokens);
            actualOverlap
                .Should()
                .Be(overlap, $"chunks {i} and {i + 1} should share {overlap} tokens");
        }
    }

    [Fact]
    public async Task ComputeHashAsync_SameContent_ReturnsSameHash()
    {
        var bytes = Encoding.UTF8.GetBytes("RAGGit dedupe content");

        using var stream1 = new MemoryStream(bytes);
        using var stream2 = new MemoryStream(bytes.ToArray());

        var hash1 = await Chunker.ComputeHashAsync(stream1);
        var hash2 = await Chunker.ComputeHashAsync(stream2);

        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeHashAsync_DifferentContent_ReturnsDifferentHash()
    {
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("Content A"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("Content B"));

        var hash1 = await Chunker.ComputeHashAsync(stream1);
        var hash2 = await Chunker.ComputeHashAsync(stream2);

        hash1.Should().NotBe(hash2);
    }

    private static string GenerateTokens(int count)
    {
        var words = Enumerable.Range(1, count).Select(i => $"token{i}");
        return string.Join(" ", words);
    }

    private static int CountOverlap(string[] previous, string[] next)
    {
        var max = Math.Min(previous.Length, next.Length);
        for (var length = max; length > 0; length--)
        {
            var suffix = previous[^length..];
            var prefix = next[..length];
            if (suffix.SequenceEqual(prefix))
            {
                return length;
            }
        }

        return 0;
    }
}
