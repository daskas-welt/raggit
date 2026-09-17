using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using RAGGit.Client.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 007-library-pagination (red-first): the footer page sequence collapses
/// long runs behind ellipses while keeping first/last/current reachable.
/// </summary>
public sealed class PageSequenceTests
{
    [Fact]
    public void SinglePage_ReturnsOnlyCurrent()
    {
        var sequence = PageSequence.For(1, 1);

        sequence.Should().ContainSingle().Which.Should().Be(new PageNumberToken(1, true));
    }

    [Fact]
    public void ShortRun_RendersUncollapsedWithCurrentMarked()
    {
        var sequence = PageSequence.For(4, 7).ToList();

        sequence.Should().HaveCount(7);
        sequence.Should().ContainNoEllipses();
        sequence[3].Should().Be(new PageNumberToken(4, true));
    }

    [Fact]
    public void FirstPage_CollapsesRightSideOnly()
    {
        var sequence = PageSequence.For(1, 100).ToList();

        sequence
            .Should()
            .Equal(
                new PageNumberToken(1, true),
                new PageNumberToken(2, false),
                new PageNumberToken(3, false),
                new EllipsisToken(),
                new PageNumberToken(100, false)
            );
    }

    [Fact]
    public void MiddlePage_CollapsesBothSides()
    {
        var sequence = PageSequence.For(50, 100).ToList();

        sequence
            .Should()
            .Equal(
                new PageNumberToken(1, false),
                new EllipsisToken(),
                new PageNumberToken(48, false),
                new PageNumberToken(49, false),
                new PageNumberToken(50, true),
                new PageNumberToken(51, false),
                new PageNumberToken(52, false),
                new EllipsisToken(),
                new PageNumberToken(100, false)
            );
    }

    [Fact]
    public void LastPage_CollapsesLeftSideOnly()
    {
        var sequence = PageSequence.For(100, 100).ToList();

        sequence
            .Should()
            .Equal(
                new PageNumberToken(1, false),
                new EllipsisToken(),
                new PageNumberToken(98, false),
                new PageNumberToken(99, false),
                new PageNumberToken(100, true)
            );
    }

    [Fact]
    public void AllCurrents_StartWithFirstEndWithLastAndNeverDuplicateEllipses()
    {
        for (var current = 1; current <= 100; current++)
        {
            var sequence = PageSequence.For(current, 100).ToList();

            sequence.First().Should().Be(new PageNumberToken(1, current == 1));
            sequence.Last().Should().Be(new PageNumberToken(100, current == 100));
            sequence
                .Zip(sequence.Skip(1), (a, b) => a is EllipsisToken && b is EllipsisToken)
                .Should()
                .NotContain(true);
            sequence.OfType<PageNumberToken>().Should().ContainSingle(t => t.IsCurrent);
        }
    }

    [Fact]
    public void OutOfRangeCurrent_ClampsIntoValidRange()
    {
        PageSequence.For(0, 100).First().Should().Be(new PageNumberToken(1, true));
        PageSequence.For(101, 100).Last().Should().Be(new PageNumberToken(100, true));
    }
}

internal static class PageSequenceTestExtensions
{
    internal static void ContainNoEllipses(
        this FluentAssertions.Collections.GenericCollectionAssertions<PageToken> assertions
    ) => assertions.NotContain(t => t is EllipsisToken);
}
