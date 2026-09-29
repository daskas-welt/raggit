using System;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// Returns the person to sign-in when the workstation rejects the session
/// mid-browse (HTTP 401). The token is already cleared by
/// <see cref="BearerDelegatingHandler"/>.
/// </summary>
public sealed class WpfSessionExpiryNavigator : ISessionExpirySink
{
    private readonly IServiceProvider _services;

    public WpfSessionExpiryNavigator(IServiceProvider services) => _services = services;

    public static Func<bool>? TryNavigateToLogin { get; set; }

    public void OnSessionExpired()
    {
        if (TryNavigateToLogin?.Invoke() != true)
        {
            return;
        }

        var session = _services.GetRequiredService<ClientSession>();
        session.Role = string.Empty;
    }
}
