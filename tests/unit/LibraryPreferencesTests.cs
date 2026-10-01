using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 007-library-pagination (red-first): the page-size preference round-trips
/// through the seam, defaults to 25, and clamps invalid stored values.
/// </summary>
public sealed class LibraryPreferencesTests
{
    [Fact]
    public void FreshStore_ReturnsDefault25()
    {
        new InMemoryLibraryPreferences().GetPageSize().Should().Be(25);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void RoundTrip_PreservesValidSizes(int size)
    {
        ILibraryPreferences preferences = new InMemoryLibraryPreferences();

        preferences.SetPageSize(size);

        preferences.GetPageSize().Should().Be(size);
    }

    [Theory]
    [InlineData(77)]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-5)]
    public void InvalidStoredValue_FallsBackToDefault(int stored)
    {
        ILibraryPreferences preferences = new InMemoryLibraryPreferences();
        preferences.SetPageSize(stored);

        preferences.GetPageSize().Should().Be(25);
    }
}
