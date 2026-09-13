using System.Threading.Tasks;
#if MAUI
using Microsoft.Extensions.DependencyInjection;
#endif

namespace RAGGit.Client.Maui;

public partial class App : Application
{
#if MAUI
    private readonly IServiceProvider? _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }
#else
    public App()
    {
        InitializeComponent();
    }
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if MAUI
        if (_services is null)
            return new Window(new AppShell());

        var configError = _services.GetService<ConfigErrorState>();
        if (configError is not null)
        {
            // Invalid launch configuration: surface the error shell; no login attempted.
            return new Window(new AppShell(_services));
        }

        var session = _services.GetRequiredService<ClientSession>();
        var store = _services.GetRequiredService<Services.ISessionTokenStore>();

        // If a still-valid session is cached from a prior sign-in, go straight to the app.
        var cachedSession = Task.Run(() => store.GetAsync()).GetAwaiter().GetResult();
        if (cachedSession is not null)
        {
            var window = new Window(new AppShell(_services));
            _ = Task.Run(async () =>
            {
                // Opportunistically refresh if the token expires within the next 15 minutes.
                var auth = _services.GetRequiredService<Services.AuthApiClient>();
                if (cachedSession.ExpiresAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(15))
                {
                    var refresh = await auth.RefreshAsync();
                    if (!refresh.IsSuccess)
                    {
                        await store.ClearAsync();
                    }
                }

                await DiscoverRoleAsync();
            });
            return window;
        }

        // No cached session: show the per-person sign-in screen.
        var auth = _services.GetRequiredService<Services.AuthApiClient>();
        Window? loginWindow = null;
        var loginViewModel = new ViewModels.LoginViewModel(
            auth,
            session,
            async () =>
            {
                await DiscoverRoleAsync();
                if (loginWindow is not null)
                {
                    loginWindow.Page = new AppShell(_services);
                }
            }
        );
        var loginView = new Views.LoginView(loginViewModel);
        loginWindow = new Window(loginView);
        return loginWindow;
#else
        return new Window(new AppShell());
#endif
    }

#if MAUI
    private async Task DiscoverRoleAsync()
    {
        if (_services is null)
            return;
        var session = _services.GetService<ClientSession>();
        var auth = _services.GetService<Services.AuthApiClient>();
        var connectionState = _services.GetService<WorkstationConnectionState>();
        if (session is null || auth is null)
            return;

        var result = await auth.GetAuthMeAsync();
        if (result.IsSuccess && result.Data is not null)
        {
            session.Role = result.Data.Role;
            session.IdentityType = result.Data.IdentityType;
            if (connectionState is not null)
            {
                connectionState.ErrorMessage = null;
                connectionState.RetryAction = null;
            }
        }
        else if (result.IsUnauthorized)
        {
            // 401 -> token cleared; show login again by surfacing the error in the shell.
            session.Role = string.Empty;
            if (connectionState is not null)
                connectionState.ErrorMessage =
                    result.ErrorMessage ?? "unauthorized — sign in again.";
            System.Diagnostics.Debug.WriteLine($"Auth 401: {result.ErrorMessage}");
        }
        else if (result.IsUnavailable)
        {
            session.Role = string.Empty;
            if (connectionState is not null)
            {
                connectionState.ErrorMessage = result.ErrorMessage ?? "AI workstation unavailable";
                connectionState.RetryAction = async () => await DiscoverRoleAsync();
            }
            System.Diagnostics.Debug.WriteLine($"Auth unavailable: {result.ErrorMessage}");
        }
        else
        {
            if (connectionState is not null)
                connectionState.ErrorMessage = result.ErrorMessage;
        }
    }
#endif
}
