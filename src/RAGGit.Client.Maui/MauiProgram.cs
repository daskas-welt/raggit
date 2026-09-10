#if MAUI
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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // THINK: Thin client keeps RAGGit.Core only — no AI/vector deps (Constitution II).
        builder.Services.AddSingleton(Services.DocumentsApiClient.CreateDefault());
        builder.Services.AddSingleton(Services.QueryApiClient.CreateDefault());
        builder.Services.AddTransient<ViewModels.LibraryViewModel>();
        builder.Services.AddTransient<ViewModels.QueryViewModel>();
        builder.Services.AddTransient<ViewModels.UploadViewModel>();

        return builder.Build();
    }
#endif

#if MAUI
    private static void RegisterSyncfusionLicense()
    {
        // Offline validation — no WAN required (Constitution IV). Key stored in .vscode/syncfusion-key.txt per opencode.json mcp env.
        string? key = null;
        var keyPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".vscode", "syncfusion-key.txt");
        foreach (var p in new[] { ".vscode/syncfusion-key.txt", keyPath })
        {
            try { if (File.Exists(p)) { key = File.ReadAllText(p).Trim(); break; } } catch {}
        }
        key ??= Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(key))
            SyncfusionLicenseProvider.RegisterLicense(key);
    }
#endif
}
