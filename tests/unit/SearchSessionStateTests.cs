using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 029-client-ux-optimization T002: the search session state carries one search
/// text per surface across page navigation. Pages and their ViewModels are
/// transient, so the text would otherwise be discarded on every visit; the
/// singleton survives navigation and dies on restart, which is the session
/// boundary (FR-003).
/// </summary>
public sealed class SearchSessionStateTests
{
    [Fact]
    public void NewState_EverySurfaceStartsEmpty()
    {
        var state = new SearchSessionState();

        state.Get(SearchSurface.Library).Should().BeEmpty();
        state.Get(SearchSurface.History).Should().BeEmpty();
        state.Get(SearchSurface.MyDocuments).Should().BeEmpty();
        state.Get(SearchSurface.People).Should().BeEmpty();
    }

    [Fact]
    public void Set_ThenGet_ReturnsTextForThatSurface()
    {
        var state = new SearchSessionState();

        state.Set(SearchSurface.Library, "quarterly report");

        state.Get(SearchSurface.Library).Should().Be("quarterly report");
    }

    [Fact]
    public void Set_OneSurface_LeavesTheOthersUntouched()
    {
        var state = new SearchSessionState();

        state.Set(SearchSurface.History, "budget");

        state.Get(SearchSurface.Library).Should().BeEmpty();
        state.Get(SearchSurface.MyDocuments).Should().BeEmpty();
        state.Get(SearchSurface.People).Should().BeEmpty();
    }

    [Theory]
    [InlineData(SearchSurface.Library)]
    [InlineData(SearchSurface.History)]
    [InlineData(SearchSurface.MyDocuments)]
    [InlineData(SearchSurface.People)]
    public void Set_OverwritesPreviousText(SearchSurface surface)
    {
        var state = new SearchSessionState();

        state.Set(surface, "first");
        state.Set(surface, "second");

        state.Get(surface).Should().Be("second");
    }

    [Fact]
    public void Set_Null_IsStoredAsEmpty()
    {
        var state = new SearchSessionState();

        state.Set(SearchSurface.Library, "report");
        state.Set(SearchSurface.Library, null);

        state.Get(SearchSurface.Library).Should().BeEmpty();
    }
}
