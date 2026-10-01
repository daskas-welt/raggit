using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RAGGit.Client.Core.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="ILauncherService"/> that stages downloaded bytes in a temp
/// directory and opens them with the platform default handler.
/// </summary>
public sealed class WpfLauncherService : ILauncherService
{
    public Task OpenAsync(string filename, byte[] content, string contentType)
    {
        var safe = string.IsNullOrWhiteSpace(filename) ? "document" : filename;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        var dir = Path.Combine(Path.GetTempPath(), "RAGGit");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, safe);
        File.WriteAllBytes(path, content);

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
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
