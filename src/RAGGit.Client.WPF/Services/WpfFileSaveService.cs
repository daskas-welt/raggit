using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using RAGGit.Client.Core.Services;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// <see cref="IFileSaveService"/> backed by the Windows save dialog. The file is
/// written to a sibling temp path and moved into place only after the bytes are
/// complete, so a cancellation or disk error never leaves a truncated target.
/// </summary>
public sealed class WpfFileSaveService : IFileSaveService
{
    public async Task<string?> SaveAsync(
        string suggestedFileName,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        var dialog = new SaveFileDialog
        {
            FileName = string.IsNullOrWhiteSpace(suggestedFileName)
                ? "document"
                : suggestedFileName,
            DefaultExt = Path.GetExtension(suggestedFileName),
            Filter = BuildFilter(contentType),
            AddExtension = true,
            OverwritePrompt = true,
            CheckPathExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var target = dialog.FileName;
        var temp = $"{target}.{Guid.NewGuid():N}.tmp";

        try
        {
            await File.WriteAllBytesAsync(temp, content, cancellationToken);
            File.Move(temp, target, overwrite: true);
            return target;
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; the target was never written.
                }
            }
        }
    }

    public Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The saved document could not be found.", path);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static string BuildFilter(string contentType) =>
        contentType switch
        {
            "application/pdf" => "PDF documents (*.pdf)|*.pdf|All files (*.*)|*.*",
            "text/plain; charset=utf-8" => "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                "Excel workbooks (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                "Word documents (*.docx)|*.docx|All files (*.*)|*.*",
            _ => "All files (*.*)|*.*",
        };
}
