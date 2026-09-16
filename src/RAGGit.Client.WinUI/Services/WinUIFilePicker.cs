using System;
using System.Threading.Tasks;
using RAGGit.Client.Maui.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RAGGit.Client.WinUI.Services;

/// <summary>
/// <see cref="IFilePicker"/> backed by <see cref="FileOpenPicker"/>.
/// The owning window handle must be set before calling <see cref="PickAsync"/>.
/// </summary>
public sealed class WinUIFilePicker : IFilePicker
{
    public static IntPtr OwnerHwnd { get; set; }

    public async Task<PickedFile?> PickAsync()
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
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add("*");

        if (OwnerHwnd != IntPtr.Zero)
        {
            InitializeWithWindow.Initialize(picker, OwnerHwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        var stream = await file.OpenStreamForReadAsync();
        return new PickedFile(file.Name, stream, file.ContentType);
    }
}
