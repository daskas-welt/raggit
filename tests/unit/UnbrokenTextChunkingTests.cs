using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Some PDF text layers carry no word breaks at all. Whitespace token counting
/// then sees a whole book as a couple of hundred "tokens" and emits one giant
/// chunk that the embedder silently truncates, so text without word breaks must
/// fall back to a character budget.
/// </summary>
public sealed class UnbrokenTextChunkingTests
{
    [Fact]
    public void ChunkText_TextWithoutWordBreaks_IsSplitIntoOrderedChunksThatLoseNoText()
    {
        // Distinct characters: with a repeated single character every prefix
        // would look like an overlap, and the reconstruction below could not
        // tell a real overlap from a coincidence.
        var random = new Random(20261027);
        var characters = new char[15_000];
        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = (char)('a' + random.Next(26));
        }

        var text = new string(characters);

        var chunks = Chunker.ChunkText(text, Guid.NewGuid());

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(c => c.Text.Length <= Chunker.MaxUnbrokenChunkChars);
        chunks.Select(c => c.Ordinal).Should().Equal(Enumerable.Range(0, chunks.Count));

        // Overlap repeats text between neighbours; dropping each repeat must
        // reconstruct the source exactly, so no character is skipped.
        var rebuilt = new StringBuilder(chunks[0].Text);
        for (var i = 1; i < chunks.Count; i++)
        {
            var previous = chunks[i - 1].Text;
            var current = chunks[i].Text;
            var overlap = 0;
            for (var n = Math.Min(previous.Length, current.Length); n > 0; n--)
            {
                if (previous.EndsWith(current[..n], StringComparison.Ordinal))
                {
                    overlap = n;
                    break;
                }
            }

            rebuilt.Append(current[overlap..]);
        }

        rebuilt.ToString().Should().Be(text);
    }
}
