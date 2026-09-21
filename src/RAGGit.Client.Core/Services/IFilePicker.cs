using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Abstraction over a platform file picker so view models stay testable and
/// free of UI-framework references in the plain net8.0 library build.
/// </summary>
public interface IFilePicker
{
    Task<PickedFile?> PickAsync();

    /// <summary>
    /// Picks multiple files in a single pass (017-multi-file-upload).
    /// Default implementation degrades to one <see cref="PickAsync"/> call so
    /// existing fakes keep working; platform pickers override with the native
    /// multi-select API. Returns an empty list (never null) on cancel.
    /// </summary>
    async Task<IReadOnlyList<PickedFile>> PickMultipleAsync()
    {
        var single = await PickAsync().ConfigureAwait(false);
        return single is null ? Array.Empty<PickedFile>() : new[] { single };
    }
}

/// <summary>
/// A file selected by the user.
/// </summary>
public sealed class PickedFile
{
    public string FileName { get; }
    public Stream Stream { get; }
    public string ContentType { get; }

    public PickedFile(string fileName, Stream stream, string contentType)
    {
        FileName = fileName;
        Stream = stream;
        ContentType = contentType;
    }
}

/// <summary>
/// No-op file picker used when the WinUI UI is not available (e.g. unit tests).
/// </summary>
public sealed class DummyFilePicker : IFilePicker
{
    public Task<PickedFile?> PickAsync() => Task.FromResult<PickedFile?>(null);

    public Task<IReadOnlyList<PickedFile>> PickMultipleAsync() =>
        Task.FromResult<IReadOnlyList<PickedFile>>(Array.Empty<PickedFile>());
}
