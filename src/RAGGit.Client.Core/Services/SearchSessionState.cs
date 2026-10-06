using System.Collections.Generic;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// The surfaces that remember a search text for the session
/// (029-client-ux-optimization, FR-003).
/// </summary>
public enum SearchSurface
{
    Library,
    History,
    MyDocuments,
    People,
}

/// <summary>
/// Holds one search text per surface across page navigation. Pages and their
/// ViewModels are transient, so a typed search would otherwise be discarded on
/// every visit — the singleton survives navigation instead, exactly as
/// <see cref="AskNavigationState"/> does for the pending prompt. It dies with
/// the process, which is the session boundary, so nothing is persisted.
/// </summary>
public sealed class SearchSessionState
{
    // Keyed by the enum itself so adding a SearchSurface value can never
    // outrun a hard-coded array length (which would throw on Get/Set).
    private readonly Dictionary<SearchSurface, string> _text = new();

    /// <summary>The current search text for <paramref name="surface"/>; empty when none.</summary>
    public string Get(SearchSurface surface) =>
        _text.TryGetValue(surface, out var text) ? text : string.Empty;

    /// <summary>
    /// Replace the search text for <paramref name="surface"/>. Null is stored
    /// as empty so a read never returns null.
    /// </summary>
    public void Set(SearchSurface surface, string? text) => _text[surface] = text ?? string.Empty;
}
