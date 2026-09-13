using System;
using System.Net.Http;
#if MAUI
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;
#endif

namespace RAGGit.Client.Maui;

public static class MauiProgram
{
#if MAUI
    public static MauiApp CreateMauiApp()
    {
        RegisterSyncfusionLicense();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
        builder.Configuration.AddUserSecrets<App>(optional: true);
#endif
        builder.Configuration.AddJsonFile(
            "appsettings.json",
            optional: true,
            reloadOnChange: false
        );

        // Resolve workstation config (FR-003) — no unauthenticated calls
        var configDict = builder
            .Configuration.AsEnumerable()
            .Where(kv => kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var startupConfig = Config.ClientConfigResolver.Resolve(
            new Dictionary<string, string?>(configDict, StringComparer.OrdinalIgnoreCase)
        );

        var session = new ClientSession
        {
            WorkstationUrl =
                startupConfig.WorkstationUrl
                ?? builder.Configuration["Workstation:Url"]
                ?? string.Empty,
            ApiKey =
                startupConfig.ApiKey
                ?? Config.ClientConfigResolver.ResolveApiKey(
                    new Dictionary<string, string?>(configDict, StringComparer.OrdinalIgnoreCase)
                )
                ?? string.Empty,
            Role = string.Empty,
            IdentityType = "ApiKey",
        };

        if (!startupConfig.IsValid)
        {
            // Surface launch config-error: never attempt HTTP
            System.Diagnostics.Debug.WriteLine($"Client config error: {startupConfig.Error}");
            builder.Services.AddSingleton(session);
            builder.Services.AddSingleton(
                new ConfigErrorState(startupConfig.Error ?? "Invalid client configuration")
            );
            builder.Services.AddSingleton(
                new WorkstationConnectionState { ErrorMessage = startupConfig.Error }
            );
            // Still register ViewModels so UI can show error (use ClientSession ctor per T022)
            builder.Services.AddTransient<ViewModels.LibraryViewModel>(
                sp => new ViewModels.LibraryViewModel(
                    new Services.DocumentsApiClient(
                        new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                    ),
                    session
                )
            );
            builder.Services.AddTransient<ViewModels.QueryViewModel>(
                sp => new ViewModels.QueryViewModel(
                    new Services.QueryApiClient(
                        new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                    )
                )
            );
            builder.Services.AddTransient<ViewModels.UploadViewModel>(
                sp => new ViewModels.UploadViewModel(
                    new Services.DocumentsApiClient(
                        new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                    ),
                    new Services.DummyFilePicker(),
                    session
                )
            );
            // Auth + Users client stubs (never called due to invalid config)
            builder.Services.AddTransient<Services.AuthApiClient>(sp => new Services.AuthApiClient(
                new HttpClient { BaseAddress = new Uri("http://invalid-config") }
            ));
            builder.Services.AddTransient<Services.UsersApiClient>(
                sp => new Services.UsersApiClient(
                    new HttpClient { BaseAddress = new Uri("http://invalid-config") }
                )
            );
            builder.Services.AddTransient<ViewModels.AdminUsersViewModel>();
            return builder.Build();
        }

        // Valid config — token cache + Bearer handler for per-person actions;
        // ApiKey handler remains registered for bootstrap-only use.
        builder.Services.AddSingleton(session);
        builder.Services.AddSingleton(new WorkstationConnectionState());
#if MAUI
        builder.Services.AddSingleton<Services.IFilePicker, Services.MauiFilePicker>();
        builder.Services.AddSingleton<Services.ISecureStorage, Services.SecureStorageAdapter>();
#else
        builder.Services.AddSingleton<Services.IFilePicker, Services.DummyFilePicker>();
        builder.Services.AddSingleton<Services.ISecureStorage, Services.InMemorySecureStorage>();
#endif
        builder.Services.AddSingleton<Services.ISessionTokenStore, Services.SessionTokenStore>();
        builder.Services.AddTransient<Services.ApiKeyDelegatingHandler>(
            _ => new Services.ApiKeyDelegatingHandler(session.ApiKey)
        );
        builder.Services.AddTransient<Services.BearerDelegatingHandler>();

        builder
            .Services.AddHttpClient<Services.DocumentsApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<Services.BearerDelegatingHandler>();

        builder
            .Services.AddHttpClient<Services.QueryApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<Services.BearerDelegatingHandler>();

        builder
            .Services.AddHttpClient<Services.AuthApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<Services.BearerDelegatingHandler>();

        builder
            .Services.AddHttpClient<Services.UsersApiClient>(client =>
            {
                client.BaseAddress = new Uri(session.WorkstationUrl);
            })
            .AddHttpMessageHandler<Services.BearerDelegatingHandler>();

        builder.Services.AddTransient<ViewModels.LoginViewModel>();
        builder.Services.AddTransient<ViewModels.LibraryViewModel>();
        builder.Services.AddTransient<ViewModels.QueryViewModel>();
        builder.Services.AddTransient<ViewModels.UploadViewModel>();
        builder.Services.AddTransient<ViewModels.AdminUsersViewModel>();
        builder.Services.AddTransient<Views.LoginView>();

        return builder.Build();
    }
#endif

#if MAUI
    private static void RegisterSyncfusionLicense()
    {
        string? key = null;
        var keyPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            ".vscode",
            "syncfusion-key.txt"
        );
        foreach (var p in new[] { ".vscode/syncfusion-key.txt", keyPath })
        {
            try
            {
                if (File.Exists(p))
                {
                    key = File.ReadAllText(p).Trim();
                    break;
                }
            }
            catch { }
        }
        key ??= Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(key))
            SyncfusionLicenseProvider.RegisterLicense(key);
    }
#endif
}

public sealed class ConfigErrorState
{
    public string Message { get; }

    public ConfigErrorState(string message) => Message = message;
}

public sealed class WorkstationConnectionState
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
