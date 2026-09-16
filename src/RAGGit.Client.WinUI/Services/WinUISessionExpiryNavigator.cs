using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.WinUI.Views;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// Returns the person to sign-in when the workstation rejects the session
/// mid-browse (HTTP 401). The token is already cleared by
/// <see cref="BearerDelegatingHandler"/>.
/// </summary>
public sealed class WinUISessionExpiryNavigator : ISessionExpirySink
{
    private readonly IServiceProvider _services;

    public WinUISessionExpiryNavigator(IServiceProvider services) => _services = services;

    public void OnSessionExpired()
    {
        var window = App.MainWindow;
        if (window is null || window.ContentFrame?.Content is LoginPage)
        {
            return;
        }

        window.DispatcherQueue.TryEnqueue(() =>
        {
            var session = _services.GetRequiredService<ClientSession>();
            session.Role = string.Empty;
            window.NavigateToLogin();
            _ = Task.CompletedTask;
        });
    }
}
