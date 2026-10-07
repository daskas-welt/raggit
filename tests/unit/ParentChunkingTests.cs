using System;
using System.Linq;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T016: Parent grouping in <see cref="Chunker"/> — groups of
/// <c>ParentGroupSize</c> consecutive children, parent text trims the
/// inter-child overlap (no duplicated sentence), trailing short groups,
/// <c>ParentGroupSize = 1</c> degrades to parent == child, and empty input
/// yields no chunks.
/// </summary>
public sealed class ParentChunkingTests
{
    private const int ChunkSize = 512;
    private const int Overlap = 50;

    [Fact]
    public void GroupIntoParents_GroupsConsecutiveChildren_AndStampsParentIds()
    {
        var documentId = Guid.NewGuid();
        var children = Chunker.ChunkText(Tokens(2500), documentId, ChunkSize, Overlap);
        children.Should().HaveCount(6);

        var parents = Chunker.GroupIntoParents(children, parentGroupSize: 4, chunkOverlap: Overlap);

        parents.Should().HaveCount(2);
        parents[0].Ordinal.Should().Be(0);
        parents[1].Ordinal.Should().Be(1);
        parents.Should().OnlyContain(p => p.Level == ChunkLevel.Parent);
        parents.Should().OnlyContain(p => p.ParentId == null);
        parents.Should().OnlyContain(p => p.DocumentId == documentId);
        parents.Select(p => p.Id).Should().OnlyHaveUniqueItems();

        children.Take(4).Should().OnlyContain(c => c.ParentId == parents[0].Id);
        children.Skip(4).Should().OnlyContain(c => c.ParentId == parents[1].Id);
        children.Should().OnlyContain(c => c.Level == ChunkLevel.Child);
    }

    [Fact]
    public void GroupIntoParents_TrimsInterChildOverlap_NoDuplicatedSentence()
    {
        var documentId = Guid.NewGuid();
        var text = Tokens(1200);
        var children = Chunker.ChunkText(text, documentId, ChunkSize, Overlap);
        children.Should().HaveCount(3);

        var parents = Chunker.GroupIntoParents(children, parentGroupSize: 4, chunkOverlap: Overlap);

        var parent = parents.Should().ContainSingle().Subject;
        // Lossless reconstruction: the trimmed concatenation is the full text.
        parent.Text.Should().Be(text);
        parent.TokenCount.Should().Be(1200);

        // The overlapped region occurs exactly once in the parent.
        CountOccurrences(parent.Text, "token500").Should().Be(1);
    }

    [Fact]
    public void GroupIntoParents_TrailingShortGroup_FormsItsOwnParent()
    {
        var documentId = Guid.NewGuid();
        // 9 children with group size 4 → 4 + 4 + 1.
        var children = Chunker.ChunkText(Tokens(4100), documentId, ChunkSize, Overlap);
        children.Should().HaveCount(9);

        var parents = Chunker.GroupIntoParents(children, parentGroupSize: 4, chunkOverlap: Overlap);

        parents.Should().HaveCount(3);
        children.Take(4).Should().OnlyContain(c => c.ParentId == parents[0].Id);
        children.Skip(4).Take(4).Should().OnlyContain(c => c.ParentId == parents[1].Id);
        children.Skip(8).Should().ContainSingle().Which.ParentId.Should().Be(parents[2].Id);
    }

    [Fact]
    public void GroupIntoParents_GroupSizeOne_ParentEqualsChild()
    {
        var documentId = Guid.NewGuid();
        var children = Chunker.ChunkText(Tokens(1200), documentId, ChunkSize, Overlap);

        var parents = Chunker.GroupIntoParents(children, parentGroupSize: 1, chunkOverlap: Overlap);

        parents.Should().HaveCount(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            parents[i].Text.Should().Be(children[i].Text);
            parents[i].Ordinal.Should().Be(i);
            children[i].ParentId.Should().Be(parents[i].Id);
        }
    }

    [Fact]
    public void GroupIntoParents_EmptyText_YieldsNoChunks()
    {
        var children = Chunker.ChunkText(string.Empty, Guid.NewGuid(), ChunkSize, Overlap);
        children.Should().BeEmpty();

        Chunker
            .GroupIntoParents(children, parentGroupSize: 4, chunkOverlap: Overlap)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void GroupIntoParents_InvalidGroupSize_Throws()
    {
        var children = Chunker.ChunkText(Tokens(100), Guid.NewGuid(), ChunkSize, Overlap);

        Action act = () =>
            Chunker.GroupIntoParents(children, parentGroupSize: 0, chunkOverlap: Overlap);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static string Tokens(int count) =>
        string.Join(" ", Enumerable.Range(1, count).Select(i => $"token{i}"));

    private static int CountOccurrences(string text, string token)
    {
        var tokens = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        return tokens.Count(t => t == token);
    }
}
