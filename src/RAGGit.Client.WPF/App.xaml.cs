using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Config;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Client.WPF.Services;
using RAGGit.Client.WPF.ViewModels;
using RAGGit.Client.WPF.Views;
using RAGGit.Client.WPF.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;

namespace RAGGit.Client.WPF;

public sealed partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static ClientSession Session => Services.GetRequiredService<ClientSession>();

    public App()
    {
        Services = ConfigureServices();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogUnhandled(e.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, e) =>
        {
            LogUnhandled(e.Exception);
            e.Handled = true;
        };
    }

    private static void LogUnhandled(Exception? exception)
    {
        if (exception is null)
            return;

        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RAGGit"
            );
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "crash.log"),
                $"{DateTimeOffset.Now:o} UNHANDLED {exception}{Environment.NewLine}"
            );
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Follow the Windows theme (Light/Dark/High Contrast) by default; the
        // Settings page can still apply an explicit override afterwards.
        ApplicationThemeManager.ApplySystemTheme();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
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
        services.AddSingleton<ConversationStore>();
        services.AddLogging(builder => builder.AddDebug());

        // WPF-UI services
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddNavigationViewPageProvider();
        services.AddSingleton<Wpf.Ui.INavigationService, Wpf.Ui.NavigationService>();

        // Platform services
        services.AddSingleton<
            RAGGit.Client.Core.Services.INavigationService,
            WpfNavigationService
        >();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<INotificationService, WpfNotificationService>();
        services.AddSingleton<ISecureStorage, WpfSecureStorage>();
        services.AddSingleton<ISessionTokenStore, SessionTokenStore>();
        services.AddSingleton<IFilePicker, WpfFilePicker>();
        services.AddSingleton<ILibraryPreferences, WpfLibraryPreferences>();
        services.AddSingleton<ILauncherService, WpfLauncherService>();
        services.AddSingleton<ISessionExpirySink, WpfSessionExpiryNavigator>();

        // Connection/error state
        services.AddSingleton(new WpfConnectionState());
        services.AddSingleton<QueryDetailNavigationState>();
        services.AddSingleton<AskNavigationState>();
        services.AddSingleton<SearchSessionState>();

        // Handlers
        services.AddTransient<ApiKeyDelegatingHandler>(_ => new ApiKeyDelegatingHandler(
            session.ApiKey
        ));
        services.AddTransient<BearerDelegatingHandler>();

        if (!startupConfig.IsValid)
        {
            services.AddSingleton(
                new WpfConfigErrorState(startupConfig.Error ?? "Invalid client configuration")
            );
            services.AddSingleton(new WpfConnectionState { ErrorMessage = startupConfig.Error });

            RegisterInvalidConfigClients(services, session);
            RegisterViewModels(services, configuration);
            RegisterPages(services);
            return services.BuildServiceProvider();
        }

        // HTTP clients
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
                client.Timeout = TimeSpan.FromMinutes(10);
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

        RegisterViewModels(services, configuration);
        RegisterPages(services);

        return services.BuildServiceProvider();
    }

    private static void RegisterInvalidConfigClients(
        IServiceCollection services,
        ClientSession session
    )
    {
        services.AddTransient<DocumentsApiClient>(_ => new DocumentsApiClient(
            new HttpClient { BaseAddress = new Uri("http://invalid-config") }
        ));
        services.AddTransient<QueryApiClient>(_ => new QueryApiClient(
            new HttpClient { BaseAddress = new Uri("http://invalid-config") }
        ));
        services.AddTransient<QueryHistoryApiClient>(_ => new QueryHistoryApiClient(
            new HttpClient { BaseAddress = new Uri("http://invalid-config") }
        ));
        services.AddTransient<AuthApiClient>(_ => new AuthApiClient(
            new HttpClient { BaseAddress = new Uri("http://invalid-config") }
        ));
        services.AddTransient<UsersApiClient>(_ => new UsersApiClient(
            new HttpClient { BaseAddress = new Uri("http://invalid-config") }
        ));
    }

    private static void RegisterViewModels(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<QueryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<QueryDetailViewModel>();
        services.AddTransient<DocumentsMineViewModel>();
        services.AddTransient<DashboardViewModel>();
        // Bounded parallel upload: the throttle comes from configuration so a
        // slow-disk box can dial it down without a rebuild.
        var uploadConcurrency = int.TryParse(
            configuration["Workstation:UploadConcurrency"],
            out var configuredConcurrency
        )
            ? configuredConcurrency
            : UploadViewModel.DefaultMaxConcurrentUploads;
        services.AddTransient(sp => new UploadViewModel(
            sp.GetRequiredService<DocumentsApiClient>(),
            sp.GetRequiredService<IFilePicker>(),
            sp.GetRequiredService<ClientSession>()
        )
        {
            MaxConcurrentUploads = UploadViewModel.ClampConcurrency(uploadConcurrency),
        });
        services.AddTransient<AdminUsersViewModel>();
        services.AddTransient<MainWindowViewModel>();
    }

    private static void RegisterPages(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddTransient<LoginPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<LibraryPage>();
        services.AddTransient<QueryPage>();
        services.AddTransient<QueryDetailPage>();
        services.AddTransient<HistoryPage>();
        services.AddTransient<DocumentsMinePage>();
        services.AddTransient<AdminUsersPage>();
        services.AddTransient<SettingsPage>();
    }
}

public sealed class WpfConfigErrorState
{
    public string Message { get; }

    public WpfConfigErrorState(string message) => Message = message;
}
