using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly INavigationService _navigation;

    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        _navigation = App.Services.GetRequiredService<INavigationService>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshRole();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnLibraryClicked(object sender, RoutedEventArgs e) =>
        _navigation.NavigateToLibrary();

    private void OnAskClicked(object sender, RoutedEventArgs e) => _navigation.NavigateToAsk();

    private void OnHistoryClicked(object sender, RoutedEventArgs e) =>
        _navigation.NavigateToHistory();

    private void OnMyDocsClicked(object sender, RoutedEventArgs e) =>
        _navigation.NavigateToMyDocs();

    private void OnAdminClicked(object sender, RoutedEventArgs e) => _navigation.NavigateToAdmin();
}
