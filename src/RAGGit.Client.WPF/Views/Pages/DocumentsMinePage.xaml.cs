using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class DocumentsMinePage : Page
{
    public DocumentsMineViewModel ViewModel { get; }

    public DocumentsMinePage()
    {
        ViewModel = App.Services.GetRequiredService<DocumentsMineViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel.Items.Count == 0 && !ViewModel.IsBusy)
            {
                _ = ViewModel.LoadCommand.ExecuteAsync(null);
            }
        };
        // Stop the status loop when this transient page is navigated away from,
        // so it never keeps polling the workstation for a discarded view.
        Unloaded += (_, _) => ViewModel.StopStatusPolling();
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e) =>
        ViewModel.SearchText = string.Empty;
}
