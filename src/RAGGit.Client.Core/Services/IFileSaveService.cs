using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Seam for saving generated document bytes to a location the employee chooses
/// through the operating system's file dialog, and opening the saved file with
/// its platform handler. Kept separate from <see cref="ILauncherService"/>
/// (which stages library downloads in a temp directory) so a generated file is
/// never written anywhere the employee did not choose.
/// </summary>
public interface IFileSaveService
{
    /// <summary>
    /// Shows the OS save dialog with <paramref name="suggestedFileName"/> and
    /// writes <paramref name="content"/> only when the employee confirms.
    /// Returns the chosen path, or null when the dialog is cancelled. The
    /// implementation must not leave a partial file behind on failure.
    /// </summary>
    Task<string?> SaveAsync(
        string suggestedFileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default
    );

    /// <summary>Opens a previously saved document with its platform handler.</summary>
    Task OpenAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Test double that records save/open calls and can simulate cancellation or
/// failure without touching the filesystem.
/// </summary>
public sealed class InMemoryFileSaveService : IFileSaveService
{
    /// <summary>Path returned by the next <see cref="SaveAsync"/>; null simulates cancel.</summary>
    public string? NextPath { get; set; } = "C:\\temp\\saved.docx";

    /// <summary>Last path a save reported; null when the last save was cancelled.</summary>
    public string? SavedPath { get; private set; }

    /// <summary>Last path passed to <see cref="OpenAsync"/>.</summary>
    public string? OpenedPath { get; private set; }

    /// <summary>When true, <see cref="SaveAsync"/> throws instead of returning a path.</summary>
    public bool FailSave { get; set; }

    /// <summary>When true, <see cref="OpenAsync"/> throws.</summary>
    public bool FailOpen { get; set; }

    public Task<string?> SaveAsync(
        string suggestedFileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        if (FailSave)
        {
            throw new IOException("save failed");
        }

        SavedPath = NextPath;
        return Task.FromResult(NextPath);
    }

    public Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        if (FailOpen)
        {
            throw new IOException("open failed");
        }

        OpenedPath = path;
        return Task.CompletedTask;
    }
}
