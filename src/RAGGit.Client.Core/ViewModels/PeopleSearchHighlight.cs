using System.Collections.Generic;
using System.Linq;
using RAGGit.Client.Core.Services;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// Decides who the People directory highlights while a search is active
/// (029-client-ux-optimization, FR-011). The first match is highlighted
/// automatically so the details panel offers that person's actions without a
/// row click; a person the user already highlighted stays highlighted while
/// they remain visible; clearing the search changes nothing.
/// </summary>
public static class PeopleSearchHighlight
{
    public static UserAccountDto? Resolve(
        string? searchText,
        UserAccountDto? current,
        IReadOnlyList<UserAccountDto> matches
    )
    {
        // No active search: the highlight is the user's, leave it alone.
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return current;
        }

        // The highlighted person is still in the results: keep them, so typing
        // does not yank the selection away from someone the user chose.
        if (current is not null && matches.Contains(current))
        {
            return current;
        }

        // Otherwise the first match, or nobody when nothing matches.
        return matches.FirstOrDefault();
    }
}
