using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Config;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Client.WinUI.Services;
using RAGGit.Client.WinUI.Views;

namespace RAGGit.Client.WinUI;

public sealed partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static ClientSession Session => Services.GetRequiredService<ClientSession>();

    public static MainWindow? MainWindow { get; set; }

    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RAGGit"
                );
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "crash.log"),
                    $"{DateTimeOffset.Now:o} UNHANDLED {e.Exception}{Environment.NewLine}"
                );
            }
            catch
            {
                // Logging must never throw.
            }
        };
        Services = ConfigureServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        var configError = Services.GetService<WinUIConfigErrorState>();
        if (configError is not null)
        {
            ((MainWindow)_window).ShowConfigError(configError.Message);
        }
        else
        {
            _ = ((MainWindow)_window).InitializeSessionAsync();
        }

        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
#if DEBUG
            .AddUserSecrets<App>(optional: true)
#endif
            .Build();

        var configDict = configuration
            .AsEnumerable()
            .Where(kv => kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var startupConfig = ClientConfigResolver.Resolve(
            new Dictionary<string, string?>(configDict, StringComparer.OrdinalIgnoreCase)
        );

        var session = new ClientSession
        {
            WorkstationUrl =
                startupConfig.WorkstationUrl ?? configuration["Workstation:Url"] ?? string.Empty,
            ApiKey =
                startupConfig.ApiKey
                ?? ClientConfigResolver.ResolveApiKey(
                    new Dictionary<string, string?>(configDict, StringComparer.OrdinalIgnoreCase)
                )
                ?? string.Empty,
            Role = string.Empty,
            IdentityType = "ApiKey",
        };

        services.AddSingleton(session);
        services.AddSingleton<IConfiguration>(configuration);

        if (!startupConfig.IsValid)
        {
            services.AddSingleton(
                new WinUIConfigErrorState(startupConfig.Error ?? "Invalid client configuration")
            );
            services.AddSingleton(new WinUIConnectionState { ErrorMessage = startupConfig.Error });
            // Register ViewModels with inert clients so the error shell can still resolve them.
            services.AddTransient<LibraryViewModel>(_ => new LibraryViewModel(
                new DocumentsApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                ),
                session,
                new DummyLauncherService()
            ));
            services.AddTransient<QueryViewModel>(_ => new QueryViewModel(
                new QueryApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                )
            ));
            services.AddTransient<HistoryViewModel>(_ => new HistoryViewModel(
                new QueryHistoryApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                )
            ));
            services.AddTransient<QueryDetailViewModel>(_ => new QueryDetailViewModel(
                new QueryHistoryApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                )
            ));
            services.AddTransient<DocumentsMineViewModel>(_ => new DocumentsMineViewModel(
                new DocumentsApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                )
            ));
            services.AddTransient<UploadViewModel>(_ => new UploadViewModel(
                new DocumentsApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                ),
                new DummyFilePicker(),
                session
            ));
            services.AddTransient<AuthApiClient>(_ => new AuthApiClient(
                new HttpClient { BaseAddress = new Uri("http://invalid-config") }
            ));
            services.AddTransient<UsersApiClient>(_ => new UsersApiClient(
                new HttpClient { BaseAddress = new Uri("http://invalid-config") }
            ));
            services.AddTransient<AdminUsersViewModel>();
            return services.BuildServiceProvider();
        }

        services.AddSingleton(new WinUIConnectionState());
        services.AddSingleton<ISecureStorage, WinUISecureStorage>();
        services.AddSingleton<ISessionTokenStore, SessionTokenStore>();
        services.AddSingleton<IFilePicker, WinUIFilePicker>();
        services.AddSingleton<ILibraryPreferences, WinUILibraryPreferences>();
        services.AddSingleton<ILauncherService, WinUILauncherService>();
        services.AddSingleton<ISessionExpirySink, WinUISessionExpiryNavigator>();
        services.AddTransient<ApiKeyDelegatingHandler>(_ => new ApiKeyDelegatingHandler(
            session.ApiKey
        ));
        services.AddTransient<BearerDelegatingHandler>();

        services
            .AddHttpClient<DocumentsApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .AddHttpMessageHandler<BearerDelegatingHandler>();
        services
            .AddHttpClient<QueryApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
                client.Timeout = TimeSpan.FromSeconds(150);
            })
            .AddHttpMessageHandler<BearerDelegatingHandler>();
        services
            .AddHttpClient<QueryHistoryApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<BearerDelegatingHandler>();
        services
            .AddHttpClient<AuthApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<BearerDelegatingHandler>();
        services
            .AddHttpClient<UsersApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<BearerDelegatingHandler>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<QueryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<QueryDetailViewModel>();
        services.AddTransient<DocumentsMineViewModel>();
        services.AddTransient<UploadViewModel>();
        services.AddTransient<AdminUsersViewModel>();

        return services.BuildServiceProvider();
    }
}

public sealed class WinUIConfigErrorState
{
    public string Message { get; }

    public WinUIConfigErrorState(string message) => Message = message;
}

public sealed class WinUIConnectionState
{
    public string? ErrorMessage { get; set; }

    public bool IsUnavailable =>
        !string.IsNullOrWhiteSpace(ErrorMessage)
        && (
            ErrorMessage.Contains("AI workstation", StringComparison.OrdinalIgnoreCase)
            || ErrorMessage.Contains("cannot reach", StringComparison.OrdinalIgnoreCase)
        );

    public Func<Task>? RetryAction { get; set; }
}
