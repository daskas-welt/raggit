using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly INavigationService _navigation;
    private bool _compactPanels;

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
        ApplyResponsiveLayout(DashboardLayout.ActualWidth);
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnDashboardLayoutSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width <= 720;
        if (compact == _compactPanels)
        {
            return;
        }

        _compactPanels = compact;
        if (compact)
        {
            PanelsGrid.ColumnDefinitions[1].Width = new GridLength(0);
            Grid.SetRow(QuestionsPanel, 1);
            Grid.SetColumn(QuestionsPanel, 0);
            DocumentsPanel.Margin = new Thickness(0, 0, 0, 8);
            QuestionsPanel.Margin = new Thickness(0);
        }
        else
        {
            PanelsGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            Grid.SetRow(QuestionsPanel, 0);
            Grid.SetColumn(QuestionsPanel, 1);
            DocumentsPanel.Margin = new Thickness(0, 0, 8, 0);
            QuestionsPanel.Margin = new Thickness(8, 0, 0, 0);
        }
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
