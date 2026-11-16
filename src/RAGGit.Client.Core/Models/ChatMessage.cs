using System;
using CommunityToolkit.Mvvm.ComponentModel;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>A single chat message (user or assistant).</summary>
public sealed class ChatMessage : ObservableObject
{
    public string AutomationKey { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public bool IsAssistant => !IsUser;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public IReadOnlyList<Citation> Citations { get; set; } = Array.Empty<Citation>();
    public bool HasCitations => Citations.Count > 0;
    public string SourcesAutomationId => $"SourcesExpander_{AutomationKey}";
    public string MessageCitationsAutomationId => $"MessageCitationsList_{AutomationKey}";

    /// <summary>The persisted query id, present on live and History answers (033).</summary>
    public Guid? QueryId { get; set; }

    private string? _savedPath;

    /// <summary>Last successful save path, used by the Open action (033).</summary>
    public string? SavedPath
    {
        get => _savedPath;
        set => SetProperty(ref _savedPath, value);
    }

    /// <summary>
    /// A document can be requested only for an owned, cited answer; refusals and
    /// citationless answers expose no save action (033 FR-016/FR-018/FR-019).
    /// </summary>
    public bool CanSave =>
        QueryId is not null
        && Citations.Count > 0
        && !string.Equals(
            Text.Trim(),
            "no relevant content found",
            StringComparison.OrdinalIgnoreCase
        );
}
