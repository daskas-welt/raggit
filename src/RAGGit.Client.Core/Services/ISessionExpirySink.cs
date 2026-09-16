namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Notified when the workstation rejects the current session (HTTP 401) so the
/// shell can clear session state and return the person to the sign-in screen
/// (006-client-architecture, spec edge case: expiry/deactivation while browsing).
/// Lives in the shared behavior layer so the 401 reaction stays testable without the UI shell.
/// </summary>
public interface ISessionExpirySink
{
    void OnSessionExpired();
}
