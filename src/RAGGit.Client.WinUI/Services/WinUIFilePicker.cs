using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// <see cref="IFilePicker"/> backed by <see cref="FileOpenPicker"/>.
/// The owning window handle must be set before calling pick methods.
/// Single pick uses <c>PickSingleFileAsync</c>; multi pick (017) uses
/// <c>PickMultipleFilesAsync</c> with the same filter list.
/// </summary>
public sealed class WinUIFilePicker : IFilePicker
{
    public static IntPtr OwnerHwnd { get; set; }

    public async Task<PickedFile?> PickAsync()
    {
        var picker = CreatePicker();
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        return await ToPickedFileAsync(file);
    }

    public async Task<IReadOnlyList<PickedFile>> PickMultipleAsync()
    {
        var picker = CreatePicker();
        var files = await picker.PickMultipleFilesAsync();
        var picked = new List<PickedFile>(files.Count);
        foreach (var file in files)
        {
            picked.Add(await ToPickedFileAsync(file));
        }
        return picked;
    }

    private static FileOpenPicker CreatePicker()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".pdf");
        picker.FileTypeFilter.Add(".docx");
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".txt");

        if (OwnerHwnd != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, OwnerHwnd);
        }

        return picker;
    }

    private static async Task<PickedFile> ToPickedFileAsync(Windows.Storage.StorageFile file)
    {
        var stream = await file.OpenStreamForReadAsync();
        return new PickedFile(file.Name, stream, file.ContentType);
    }
}
