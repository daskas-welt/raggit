using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace RAGGit.Client.WinUI.Views;

/// <summary>
/// Multi-file upload dialog (015-file-upload-ui + 017-multi-file-upload):
/// click-to-select multi-pass picking and a drag-and-drop target both feed
/// the same queue with per-file progress plus an overall completed count.
/// Full success shows a brief success outcome then auto-closes (the Library
/// refreshes behind it); any failure or cancel stays open with per-file
/// outcomes. The queue (pick, drop, remove) locks while uploading.
/// Dismissing mid-upload cancels the in-flight operation. Focus moves inside
/// on open; <see cref="LibraryPage"/> returns focus to the Upload button
/// on close.
/// </summary>
public sealed partial class UploadDialog : ContentDialog
{
    private bool _autoCloseArmed;

    public UploadViewModel ViewModel { get; }

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
        AddFilesButton.Focus(FocusState.Programmatic);
    }

    private async void OnPrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        // ContentDialog normally dismisses itself after executing a primary
        // command. Upload must remain visible while the HTTP request runs;
        // otherwise Closing cancels the request immediately and the TLS
        // stack reports TaskCanceledException/ObjectDisposedException.
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try
        {
            if (ViewModel.IsUploading)
            {
                ViewModel.CancelUploadCommand.Execute(null);
            }
            else
            {
                await ViewModel.UploadCommand.ExecuteAsync(null);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // Dismiss during upload (Esc, backdrop, close control) cancels the
        // in-flight operation so no orphaned partial document appears. The
        // deferral keeps the dialog alive until the upload task has unwound;
        // disposing queued streams any earlier races the HTTP stack aborting
        // the TLS connection (ObjectDisposedException on SslStream).
        // The VM is transient per dialog open; release queued file handles.
        if (ViewModel.IsUploading)
        {
            var deferral = args.GetDeferral();
            try
            {
                ViewModel.CancelUploadCommand.Execute(null);
                var run = ViewModel.UploadTask;
                if (run is not null)
                {
                    // Do not dispose the queued streams on a timeout. The
                    // request may still be unwinding its TLS connection;
                    // disposing the upload stream here reproduces the
                    // SslStream ObjectDisposedException. Cancellation is
                    // propagated to HttpClient, so this completes promptly
                    // once the transport has actually stopped.
                    try
                    {
                        await run;
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation is the expected close path.
                    }
                }
            }
            finally
            {
                ViewModel.Dispose();
                deferral.Complete();
            }
        }
        else
        {
            ViewModel.Dispose();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
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
        // screen readers via the LiveSetting InfoBar) before close.
        await Task.Delay(TimeSpan.FromMilliseconds(900));
        if (_autoCloseArmed && ViewModel.IsAllSucceeded && !ViewModel.IsUploading)
        {
            DispatcherQueue.TryEnqueue(Hide);
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
        if (ViewModel.IsDropEnabled && e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to add to the upload queue";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            ViewModel.IsDragOver = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
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
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                ViewModel.ReportDropError("Drop files to add them to the queue.");
                return;
            }

            var items = await e.DataView.GetStorageItemsAsync();
            var files = new List<PickedFile>();
            var nonFiles = new List<string>();
            foreach (var item in items)
            {
                if (item is StorageFile storageFile)
                {
                    try
                    {
                        var stream = await storageFile.OpenStreamForReadAsync();
                        files.Add(
                            new PickedFile(storageFile.Name, stream, storageFile.ContentType)
                        );
                    }
                    catch (Exception)
                    {
                        nonFiles.Add(storageFile.Name);
                    }
                }
                else
                {
                    nonFiles.Add(item.Name);
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
}
