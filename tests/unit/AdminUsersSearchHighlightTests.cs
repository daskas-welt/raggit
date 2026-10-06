using System.Collections.Generic;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 029-client-ux-optimization T017: while a people search is active, the first
/// match is highlighted automatically so the details panel offers that
/// person's actions without a row click (FR-011, SC-007). A keystroke never
/// leaves a filtered-out person highlighted, and clearing the search leaves
/// the highlight unchanged.
/// </summary>
public sealed class AdminUsersSearchHighlightTests
{
    private readonly UserAccountDto _ada = Person("Ada");
    private readonly UserAccountDto _bea = Person("Bea");

    [Fact]
    public void Search_HighlightsTheFirstMatch()
    {
        var highlight = PeopleSearchHighlight.Resolve(
            searchText: "a",
            current: null,
            matches: new[] { _ada, _bea }
        );

        highlight.Should().Be(_ada);
    }

    [Fact]
    public void Search_CurrentMatchStillVisible_KeepsIt()
    {
        var highlight = PeopleSearchHighlight.Resolve(
            searchText: "a",
            current: _bea,
            matches: new[] { _ada, _bea }
        );

        highlight.Should().Be(_bea);
    }

    [Fact]
    public void Search_CurrentFilteredOut_MovesToTheFirstMatch()
    {
        var highlight = PeopleSearchHighlight.Resolve(
            searchText: "ada",
            current: _bea,
            matches: new[] { _ada }
        );

        highlight.Should().Be(_ada);
    }

    [Fact]
    public void Search_NoMatches_ClearsTheHighlight()
    {
        var highlight = PeopleSearchHighlight.Resolve(
            searchText: "zed",
            current: _ada,
            matches: new List<UserAccountDto>()
        );

        highlight.Should().BeNull();
    }

    [Fact]
    public void ClearingTheSearch_LeavesTheHighlightUnchanged()
    {
        var highlight = PeopleSearchHighlight.Resolve(
            searchText: "",
            current: _bea,
            matches: new[] { _ada, _bea }
        );

        highlight.Should().Be(_bea);
    }

    private static UserAccountDto Person(string name) =>
        new()
        {
            Id = System.Guid.NewGuid(),
            Username = name.ToLowerInvariant(),
            DisplayName = name,
        };
}
