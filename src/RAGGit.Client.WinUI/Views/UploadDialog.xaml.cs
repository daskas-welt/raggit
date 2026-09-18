using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

/// <summary>
/// Multi-file upload dialog (015-file-upload-ui): click-to-select queue with
/// per-file progress plus an overall completed count. Full success shows a
/// brief success outcome then auto-closes (the Library refreshes behind it);
/// any failure or cancel stays open with per-file outcomes. Dismissing
/// mid-upload cancels the in-flight operation. Focus moves inside on open;
/// <see cref="LibraryPage"/> returns focus to the Upload button on close.
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

    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        // Dismiss during upload (Esc, backdrop, close control) cancels the
        // in-flight operation so no orphaned partial document appears.
        if (ViewModel.IsUploading)
        {
            ViewModel.CancelUploadCommand.Execute(null);
        }
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        // The VM is transient per dialog open; release queued file handles.
        ViewModel.Dispose();
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
}
