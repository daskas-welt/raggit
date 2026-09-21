using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class HistoryPage : Page
{
    public HistoryViewModel ViewModel { get; }

    public HistoryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<HistoryViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Items.Count == 0 && !ViewModel.IsBusy)
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void OnItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HistoryItem item)
        {
            Frame.Navigate(typeof(QueryDetailPage), item.Id);
        }
    }

    private void OnViewClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HistoryItem item)
        {
            Frame.Navigate(typeof(QueryDetailPage), item.Id);
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
                Frame.Navigate(typeof(QueryPage), detail.Prompt);
                return;
            }
        }
        catch
        {
            // Fall through to the detail page so the failure is visible
            // instead of silently doing nothing.
        }

        Frame.Navigate(typeof(QueryDetailPage), item.Id);
    }
}
