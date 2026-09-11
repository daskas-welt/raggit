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
        var window = new Window(new AppShell());
        // Launch-time role discovery (T021): call GET /api/auth/me once, populate ClientSession.IsAdmin
        _ = Task.Run(async () => await DiscoverRoleAsync());
        return window;
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
        var configError = _services.GetService<ConfigErrorState>();
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
            // 401 -> configuration error UI (never unauthenticated)
            session.Role = string.Empty;
            if (connectionState is not null)
                connectionState.ErrorMessage =
                    result.ErrorMessage ?? "unauthorized — check Workstation:ApiKey / Api:AdminKey";
            // ConfigErrorState is already registered for initial invalid config; for 401 we surface via connectionState
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
