using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Models;

/// <summary>A single chat message (user or assistant).</summary>
public sealed class ChatMessage
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
}
