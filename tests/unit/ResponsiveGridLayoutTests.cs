using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Query History tile grid (029): the available width maps to a 1–3 column
/// count so tiles reflow rather than clip at the minimum window size.
/// </summary>
public sealed class ResponsiveGridLayoutTests
{
    [Theory]
    [InlineData(1400, 3)]
    [InlineData(900, 3)]
    [InlineData(899, 2)]
    [InlineData(560, 2)]
    [InlineData(559, 1)]
    [InlineData(320, 1)]
    [InlineData(0, 1)]
    public void ColumnsFor_MapsWidthToColumns(double width, int expected) =>
        ResponsiveGridLayout.ColumnsFor(width).Should().Be(expected);

    [Fact]
    public void ColumnsFor_UnknownWidth_DefaultsToThreeColumns() =>
        ResponsiveGridLayout.ColumnsFor(double.NaN).Should().Be(3);

    [Theory]
    [InlineData(1920, 4)]
    [InlineData(1200, 4)]
    [InlineData(1199, 3)]
    [InlineData(900, 3)]
    [InlineData(899, 2)]
    [InlineData(560, 2)]
    [InlineData(559, 1)]
    [InlineData(0, 1)]
    public void ColumnsFor_WithFourColumnCap_MapsWidthToColumns(double width, int expected) =>
        ResponsiveGridLayout.ColumnsFor(width, 4).Should().Be(expected);

    [Fact]
    public void ColumnsFor_FourColumnCap_UnknownWidth_DefaultsToFourColumns() =>
        ResponsiveGridLayout.ColumnsFor(double.NaN, 4).Should().Be(4);

    [Fact]
    public void ColumnsFor_NeverExceedsRequestedMaximum() =>
        ResponsiveGridLayout.ColumnsFor(2000, 2).Should().Be(2);

    [Theory]
    [InlineData(1920, 6)]
    [InlineData(1200, 6)]
    [InlineData(1199, 3)]
    [InlineData(900, 3)]
    [InlineData(899, 2)]
    [InlineData(560, 2)]
    [InlineData(559, 1)]
    [InlineData(0, 1)]
    public void ColumnsFor_WithSixColumnCap_MapsWidthToColumns(double width, int expected) =>
        ResponsiveGridLayout.ColumnsFor(width, 6).Should().Be(expected);

    [Fact]
    public void ColumnsFor_SixColumnCap_UnknownWidth_DefaultsToSixColumns() =>
        ResponsiveGridLayout.ColumnsFor(double.NaN, 6).Should().Be(6);
}
