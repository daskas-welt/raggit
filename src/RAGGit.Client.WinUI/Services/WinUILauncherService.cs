using System;
using System.IO;
using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;
using Windows.Storage;
using Windows.System;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// <see cref="ILauncherService"/> that stages the download in the app cache
/// folder and opens it with the default handler.
/// </summary>
public sealed class WinUILauncherService : ILauncherService
{
    public async Task OpenAsync(string filename, byte[] content, string contentType)
    {
        var safe = string.IsNullOrWhiteSpace(filename) ? "document" : filename;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        // TemporaryFolder needs package identity; fall back to a temp subdir
        // for unpackaged runs.
        var dir = WinUIPackaging.IsPackaged
            ? ApplicationData.Current.TemporaryFolder.Path
            : Path.Combine(Path.GetTempPath(), "RAGGit");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, safe);
        await File.WriteAllBytesAsync(path, content);
        var file = await StorageFile.GetFileFromPathAsync(path);
        _ = await Launcher.LaunchFileAsync(file);
    }
}

/// <summary>
/// Inert launcher for the invalid-config path (never invoked).
/// </summary>
public sealed class DummyLauncherService : ILauncherService
{
    public Task OpenAsync(string filename, byte[] content, string contentType) =>
        Task.CompletedTask;
}
