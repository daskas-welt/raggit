namespace RAGGit.Client.Core.Models;

/// <summary>A single chat message (user or assistant).</summary>
public sealed class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public bool IsAssistant => !IsUser;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
