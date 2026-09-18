using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Refresh on every visit so newly uploaded documents appear.
        // Guarded by IsBusy to avoid overlap (mirrors MAUI OnAppearing).
        if (!ViewModel.IsBusy)
        {
            ViewModel.RefreshRole();
            ViewModel.LoadDocumentsCommand.Execute(null);
        }
    }

    private async void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdmin)
        {
            return;
        }

        var uploadViewModel = App.Services.GetService<UploadViewModel>();
        if (uploadViewModel is null)
        {
            return;
        }

        uploadViewModel.RefreshRole();
        var dialog = new UploadDialog(uploadViewModel) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();

        // The dialog only uploads: reload so new rows appear. On partial
        // success this picks up exactly the documents that succeeded
        // (015-file-upload-ui FR-013); failures never created server rows.
        if (!ViewModel.IsBusy)
        {
            await ViewModel.LoadDocumentsCommand.ExecuteAsync(null);
        }

        UploadButton.Focus(FocusState.Programmatic);
    }

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is Document document)
        {
            await ViewModel.DownloadAndOpenCommand.ExecuteAsync(document);
        }
    }

    private async void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not Document document)
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Delete Document",
            Content = $"Are you sure you want to delete '{document.Filename}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RowActionCommand.ExecuteAsync(document);
        }
    }
}
