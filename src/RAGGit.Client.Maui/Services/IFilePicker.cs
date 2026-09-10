using System.IO;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Abstraction over a platform file picker so view models stay testable and
/// free of MAUI-specific references in the net8.0 fallback build.
/// </summary>
public interface IFilePicker
{
    Task<PickedFile?> PickAsync();
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
/// No-op file picker used when the MAUI UI is not available (e.g. net8.0
/// fallback build or unit tests).
/// </summary>
public sealed class DummyFilePicker : IFilePicker
{
    public Task<PickedFile?> PickAsync() => Task.FromResult<PickedFile?>(null);
}
