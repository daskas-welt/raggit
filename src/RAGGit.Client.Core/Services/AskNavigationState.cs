namespace RAGGit.Client.Core.Services;

/// <summary>
/// Holds the pending ask-again prompt for <c>QueryPage</c> navigation.
/// Mirrors <c>QueryDetailNavigationState</c>: History and QueryDetail write
/// the original question here before navigating to Ask, and the Ask page
/// reads it on load and populates the input (never auto-sends). Pages are
/// transient, so a preset on a freshly resolved ViewModel would be
/// discarded — the singleton survives navigation instead.
/// </summary>
public sealed class AskNavigationState
{
    private string? _pendingPrompt;

    /// <summary>
    /// The pending prompt. A single read returns the written value and
    /// clears it; every later read returns null until written again.
    /// </summary>
    public string? PendingPrompt
    {
        get
        {
            var prompt = _pendingPrompt;
            _pendingPrompt = null;
            return prompt;
        }
        set => _pendingPrompt = value;
    }
}
