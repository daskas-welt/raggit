using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Dialogs;

/// <summary>
/// Multi-file upload dialog (015-file-upload-ui + 017-multi-file-upload):
/// click-to-select multi-pass picking and a drag-and-drop target both feed
/// the same queue with per-file progress plus an overall completed count.
/// Full success shows a brief success outcome then auto-closes; any failure
/// or cancel stays open with per-file outcomes. The queue (pick, drop,
/// remove) locks while uploading. Closing mid-upload cancels the in-flight
/// operation. Focus moves inside on open.
/// </summary>
public partial class UploadDialog : Window
{
    private bool _autoCloseArmed;
    private bool _closeAfterCancel;
    private bool _cancelConfirmed;

    public UploadViewModel ViewModel { get; }

    public UploadDialog()
        : this(App.Services.GetRequiredService<UploadViewModel>()) { }

    public UploadDialog(UploadViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closing += OnClosing;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AddFilesButton.Focus();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsUploading && !_cancelConfirmed)
        {
            ShowCancelConfirm();
            return;
        }

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_cancelConfirmed)
        {
            ShowCancelConfirm();
            return;
        }

        // Confirmed: Closing cancels the in-flight upload (see OnClosing).
        Close();
    }

    private void ConfirmCancelUploadButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelConfirmed = true;
        HideCancelConfirm();
        Close();
    }

    private void KeepUploadingButton_Click(object sender, RoutedEventArgs e)
    {
        // Declining resumes the upload view untouched.
        HideCancelConfirm();
        CancelUploadButton.Focus();
    }

    private void ShowCancelConfirm()
    {
        CancelConfirmPanel.Visibility = Visibility.Visible;
        KeepUploadingButton.Focus();
    }

    private void HideCancelConfirm() => CancelConfirmPanel.Visibility = Visibility.Collapsed;

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        // Closing mid-upload asks first through the in-window confirm panel:
        // this modal window sits over the MainWindow dialog host, so the
        // shared in-window confirm cannot appear above it (and a native
        // message box is forbidden). Declining keeps the upload untouched.
        if (ViewModel.IsUploading && !_closeAfterCancel && !_cancelConfirmed)
        {
            e.Cancel = true;
            ShowCancelConfirm();
            return;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Dismiss during upload cancels the in-flight operation so no
        // orphaned partial document appears. WPF Closing cannot defer, so
        // the first close cancels and re-closes once the upload task has
        // unwound; queued streams are only disposed then, so the HTTP stack
        // never races a disposed stream (ObjectDisposedException on SslStream).
        // The VM is transient per dialog open; release queued file handles.
        if (ViewModel.IsUploading && !_closeAfterCancel)
        {
            e.Cancel = true;
            ViewModel.CancelUploadCommand.Execute(null);
            var run = ViewModel.UploadTask;
            if (run is not null)
            {
                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected close path.
                }
            }

            ViewModel.Dispose();
            _closeAfterCancel = true;
            Close();
            return;
        }

        ViewModel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UploadViewModel.IsUploading) && !ViewModel.IsUploading)
        {
            // The upload settled while the cancel confirm was open: drop the
            // panel so it never lingers over a settled view.
            HideCancelConfirm();
        }

        if (
            e.PropertyName == nameof(UploadViewModel.IsAllSucceeded)
            && ViewModel.IsAllSucceeded
            && !_autoCloseArmed
        )
        {
            _autoCloseArmed = true;
            _ = AutoCloseAfterSuccessAsync();
        }
    }

    private async Task AutoCloseAfterSuccessAsync()
    {
        // Brief success outcome so the result is perceivable (including by
        // screen readers via the live InfoBar) before close.
        await Task.Delay(TimeSpan.FromMilliseconds(900));
        if (_autoCloseArmed && ViewModel.IsAllSucceeded && !ViewModel.IsUploading)
        {
            Dispatcher.Invoke(Close);
        }
        else
        {
            _autoCloseArmed = false;
        }
    }

    /// <summary>
    /// Drag-and-drop intake (017): view-only mechanics. Accepts OS file drops
    /// only when the queue is unlocked; all validation and state live in the
    /// ViewModel so both paths behave identically.
    /// </summary>
    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.IsDropEnabled && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            ViewModel.IsDragOver = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            ViewModel.IsDragOver = false;
        }
        e.Handled = true;
    }

    private void DropArea_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
    }

    private async void DropArea_Drop(object sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
        if (!ViewModel.IsDropEnabled)
        {
            return;
        }

        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                ViewModel.ReportDropError("Drop files to add them to the queue.");
                return;
            }

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
            var files = new List<PickedFile>();
            var nonFiles = new List<string>();
            foreach (var path in paths)
            {
                var name = Path.GetFileName(path);
                if (Directory.Exists(path) || string.IsNullOrWhiteSpace(name))
                {
                    nonFiles.Add(string.IsNullOrWhiteSpace(name) ? path : name);
                    continue;
                }

                try
                {
                    var stream = File.OpenRead(path);
                    files.Add(
                        new PickedFile(name, stream, ContentTypeFor(Path.GetExtension(path)))
                    );
                }
                catch (Exception)
                {
                    nonFiles.Add(name);
                }
            }

            await ViewModel.AddPickedFilesAsync(files);
            ViewModel.ReportRejectedNames(nonFiles);
        }
        catch (Exception)
        {
            ViewModel.ReportDropError("Could not read the dropped files.");
        }
    }

    private static string ContentTypeFor(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
}
