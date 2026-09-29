using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Client.WPF.Views.Dialogs;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Refresh on every visit so newly uploaded documents appear.
        // Guarded by IsBusy to avoid overlap (mirrors the WinUI OnNavigatedTo).
        ViewModel.RefreshRole();
        if (!ViewModel.IsBusy)
        {
            await ViewModel.LoadDocumentsCommand.ExecuteAsync(null);
        }
    }

    private async void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdmin)
        {
            return;
        }

        var dialog = new UploadDialog();
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow is not null)
        {
            dialog.Owner = mainWindow;
        }

        dialog.ShowDialog();

        // The dialog only uploads: reload so new rows appear. On partial
        // success this picks up exactly the documents that succeeded;
        // failures never created server rows.
        if (!ViewModel.IsBusy)
        {
            await ViewModel.LoadDocumentsCommand.ExecuteAsync(null);
        }

        UploadButton.Focus();
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

        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            !await dialogs.ConfirmAsync(
                "Delete Document",
                $"Are you sure you want to delete '{document.Filename}'?"
            )
        )
        {
            return;
        }

        await ViewModel.RowActionCommand.ExecuteAsync(document);
    }
}
