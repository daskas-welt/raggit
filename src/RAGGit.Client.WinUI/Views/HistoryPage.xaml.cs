using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
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
}
