using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="IFilePicker"/> backed by the WPF <see cref="OpenFileDialog"/>.
/// </summary>
public sealed class WpfFilePicker : IFilePicker
{
    public Task<PickedFile?> PickAsync()
    {
        var dialog = CreateDialog();
        dialog.Multiselect = false;

        if (dialog.ShowDialog() != true)
        {
            return Task.FromResult<PickedFile?>(null);
        }

        var file = dialog.FileName;
        var stream = File.OpenRead(file);
        return Task.FromResult<PickedFile?>(
            new PickedFile(Path.GetFileName(file), stream, GetContentType(file))
        );
    }

    public Task<IReadOnlyList<PickedFile>> PickMultipleAsync()
    {
        var dialog = CreateDialog();
        dialog.Multiselect = true;

        if (dialog.ShowDialog() != true)
        {
            return Task.FromResult<IReadOnlyList<PickedFile>>(Array.Empty<PickedFile>());
        }

        var picked = new List<PickedFile>(dialog.FileNames.Length);
        foreach (var file in dialog.FileNames)
        {
            var stream = File.OpenRead(file);
            picked.Add(new PickedFile(Path.GetFileName(file), stream, GetContentType(file)));
        }

        return Task.FromResult<IReadOnlyList<PickedFile>>(picked);
    }

    private static OpenFileDialog CreateDialog()
    {
        return new OpenFileDialog
        {
            Title = "Select documents",
            Filter = "Documents|*.pdf;*.docx;*.xlsx;*.txt|All files|*.*",
        };
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
    }
}
