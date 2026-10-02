using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// Transient workstation-connection snapshot. Starts as the startup-config
/// error snapshot and is refreshed on demand by the Settings re-check, which
/// writes the outcome of the existing <c>GET /api/auth/me</c> probe. The
/// severity/icon mapping ("cannot reach AI workstation" pattern) stays as-is.
/// </summary>
public sealed class WpfConnectionState : INotifyPropertyChanged
{
    private string? _errorMessage;
    private DateTimeOffset? _lastCheckedAtUtc;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (!string.Equals(_errorMessage, value, StringComparison.Ordinal))
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsUnavailable));
            }
        }
    }

    /// <summary>
    /// When the connection was last probed (startup snapshot has none until
    /// the first re-check). Shown as secondary text on the Settings status row.
    /// </summary>
    public DateTimeOffset? LastCheckedAtUtc
    {
        get => _lastCheckedAtUtc;
        set
        {
            if (_lastCheckedAtUtc != value)
            {
                _lastCheckedAtUtc = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsUnavailable =>
        !string.IsNullOrWhiteSpace(ErrorMessage)
        && (
            ErrorMessage.Contains("AI workstation", StringComparison.OrdinalIgnoreCase)
            || ErrorMessage.Contains("cannot reach", StringComparison.OrdinalIgnoreCase)
        );

    public Func<Task>? RetryAction { get; set; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
