using System.Collections.ObjectModel;
using RAGGit.Client.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Login-scoped in-memory conversation cache. The Ask page is recreated on
/// every navigation (transient ViewModel), so asked questions would vanish
/// whenever the server history round-trip fails or is unavailable (e.g. legacy
/// callers). The store keeps the current login's messages alive across page
/// visits; a new login (new owner key) clears it. Messages are shared by
/// reference with the ViewModel: treat them as immutable after creation
/// (mutating one side is visible on the other).
/// </summary>
public sealed class ConversationStore
{
    private string? _ownerKey;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    /// <summary>
    /// True once the employee cleared the conversation for this owner. A cleared
    /// conversation is not re-seeded from the server archive on the next Ask
    /// visit; a new login resets the flag.
    /// </summary>
    public bool IsCleared { get; private set; }

    /// <summary>
    /// Switches ownership to <paramref name="ownerKey"/>, clearing messages
    /// from a previous login. No-op when the owner is unchanged.
    /// </summary>
    public void EnsureOwner(string? ownerKey)
    {
        if (string.Equals(_ownerKey, ownerKey, StringComparison.Ordinal))
        {
            return;
        }

        _ownerKey = ownerKey;
        IsCleared = false;
        Messages.Clear();
    }

    /// <summary>
    /// Replaces the cached messages (e.g. with the server history load).
    /// </summary>
    public void ReplaceAll(IEnumerable<ChatMessage> messages)
    {
        Messages.Clear();
        foreach (var message in messages)
        {
            Messages.Add(message);
        }
    }

    /// <summary>
    /// Empties the conversation and stops it being restored from the archive
    /// for the rest of this login. Persisted server history is untouched.
    /// </summary>
    public void MarkCleared()
    {
        Messages.Clear();
        IsCleared = true;
    }
}
