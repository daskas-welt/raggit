using System.Collections.Generic;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Severity of a transient user-facing notification.
/// </summary>
public enum NotificationKind
{
    Success,
    Information,
    Danger,
}

/// <summary>
/// Seam for brief, non-blocking user feedback (copy confirmations, download
/// outcomes). The WPF shell adapts the already-wired snackbar; tests use
/// <see cref="InMemoryNotificationService"/>.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a transient, self-dismissing notification. Never blocks or
    /// displaces page content.
    /// </summary>
    void Show(string title, string message, NotificationKind kind);
}

/// <summary>
/// Test double recording every shown notification instead of displaying anything.
/// </summary>
public sealed class InMemoryNotificationService : INotificationService
{
    public sealed record ShownNotification(string Title, string Message, NotificationKind Kind);

    public readonly List<ShownNotification> Shown = new();

    public void Show(string title, string message, NotificationKind kind)
    {
        Shown.Add(new ShownNotification(title, message, kind));
    }
}
