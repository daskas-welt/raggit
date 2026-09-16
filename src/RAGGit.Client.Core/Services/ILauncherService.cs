using System.Collections.Generic;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Seam for saving downloaded bytes locally and opening them externally.
/// The WinUI shell adapts <c>StorageFile</c>/<c>Launcher</c>; tests use
/// <see cref="InMemoryLauncherService"/>.
/// </summary>
public interface ILauncherService
{
    /// <summary>
    /// Saves <paramref name="content"/> under <paramref name="filename"/> in
    /// app-local storage and opens it with the platform handler.
    /// </summary>
    Task OpenAsync(string filename, byte[] content, string contentType);
}

/// <summary>
/// Test double recording every open request instead of launching anything.
/// </summary>
public sealed class InMemoryLauncherService : ILauncherService
{
    public readonly List<(string Filename, byte[] Content, string ContentType)> Opened = new();

    public Task OpenAsync(string filename, byte[] content, string contentType)
    {
        Opened.Add((filename, content, contentType));
        return Task.CompletedTask;
    }
}
