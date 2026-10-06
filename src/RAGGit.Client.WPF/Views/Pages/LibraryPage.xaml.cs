using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Client.WPF.Views.Dialogs;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class LibraryPage : Page
{
    private bool _compactTable;

    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        // Stop the status loop when this transient page is navigated away from,
        // so it never keeps polling the workstation for a discarded view.
        Unloaded += (_, _) => ViewModel.StopStatusPolling();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Refresh on every visit so newly uploaded documents appear.
        // Guarded by IsBusy to avoid overlap (mirrors the WinUI OnNavigatedTo).
        ViewModel.RefreshRole();
        ApplyResponsiveLayout(LibraryLayout.ActualWidth);
        if (!ViewModel.IsBusy)
        {
            await ViewModel.LoadDocumentsCommand.ExecuteAsync(null);
        }
    }

    private void OnLibraryLayoutSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout(e.NewSize.Width);

    /// <summary>
    /// Page-owned responsive switch (Dashboard/Admin pattern): at content widths
    /// ≤720 DIPs the secondary table columns (Creator, Created) collapse and the
    /// 880-DIP floor is removed, so Filename, Status, and the row actions fit
    /// without horizontal scrolling; wider widths restore every column and the
    /// floor. The header row and the row template share the same resource keys,
    /// so both states stay consistent per state.
    /// </summary>
    private void ApplyResponsiveLayout(double width)
    {
        var compact = width <= 720;
        if (compact == _compactTable)
        {
            return;
        }

        _compactTable = compact;
        if (compact)
        {
            // Type and Size join Creator and Created below 720 DIPs, leaving
            // Filename, Status, and the row actions (029, FR-009).
            Resources["LibraryTypeColumnWidth"] = new GridLength(0);
            Resources["LibrarySizeColumnWidth"] = new GridLength(0);
            Resources["LibraryCreatorColumnWidth"] = new GridLength(0);
            Resources["LibraryCreatedColumnWidth"] = new GridLength(0);
            Resources["LibrarySecondaryColumnVisibility"] = Visibility.Collapsed;
            TableGrid.MinWidth = 0;
        }
        else
        {
            Resources["LibraryTypeColumnWidth"] = new GridLength(70);
            Resources["LibrarySizeColumnWidth"] = new GridLength(90);
            Resources["LibraryCreatorColumnWidth"] = new GridLength(130);
            Resources["LibraryCreatedColumnWidth"] = new GridLength(150);
            Resources["LibrarySecondaryColumnVisibility"] = Visibility.Visible;
            TableGrid.MinWidth = 880;
        }
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e) =>
        ViewModel.SearchText = string.Empty;

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
