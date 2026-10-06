using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel.Items.Count == 0 && !ViewModel.IsBusy)
            {
                _ = ViewModel.LoadCommand.ExecuteAsync(null);
            }
        };
    }

    private void OnClearSearchClicked(object sender, RoutedEventArgs e) =>
        ViewModel.SearchText = string.Empty;

    private void OnListDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if ((sender as ListView)?.SelectedItem is HistoryItem item)
        {
            App.Services.GetRequiredService<INavigationService>().NavigateToQueryDetail(item.Id);
        }
    }

    private void OnViewClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HistoryItem item)
        {
            App.Services.GetRequiredService<INavigationService>().NavigateToQueryDetail(item.Id);
        }
    }

    private async void OnAskAgainClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HistoryItem item)
        {
            return;
        }

        // History rows carry only previews; fetch the full prompt so the
        // re-run matches the original query exactly.
        try
        {
            var history = App.Services.GetRequiredService<QueryHistoryApiClient>();
            var detail = await history.GetDetailAsync(item.Id);
            if (!string.IsNullOrWhiteSpace(detail.Prompt))
            {
                // Pages are transient, so the prompt travels in the shared
                // pending-ask state; QueryPage picks it up on load.
                App.Services.GetRequiredService<AskNavigationState>().PendingPrompt = detail.Prompt;
                App.Services.GetRequiredService<INavigationService>().NavigateToAsk();
                return;
            }
        }
        catch
        {
            // Fall through to the detail page so the failure is visible
            // instead of silently doing nothing.
        }

        App.Services.GetRequiredService<INavigationService>().NavigateToQueryDetail(item.Id);
    }
}
