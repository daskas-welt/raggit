#if !NET8_0
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// MAUI implementation of <see cref="IFilePicker"/> using the platform
/// <c>FilePicker</c>.
/// </summary>
public sealed class MauiFilePicker : IFilePicker
{
    public async Task<PickedFile?> PickAsync()
    {
        var result = await FilePicker.Default.PickAsync(
            new PickOptions { PickerTitle = "Select a document" }
        );

        if (result is null)
        {
            return null;
        }

        var stream = await result.OpenReadAsync();
        return new PickedFile(
            result.FileName,
            stream,
            result.ContentType ?? "application/octet-stream"
        );
    }
}
#endif
