using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshRole();
        _ = ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnLibraryClicked(object sender, RoutedEventArgs e) =>
        App.MainWindow?.NavigateToLibrary();

    private void OnAskClicked(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.NavigateToQuery();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width <= 720;

        HeaderGrid.ColumnDefinitions.Clear();
        HeaderGrid.RowDefinitions.Clear();
        MetricsGrid.ColumnDefinitions.Clear();
        MetricsGrid.RowDefinitions.Clear();
        ContentGrid.ColumnDefinitions.Clear();
        ContentGrid.RowDefinitions.Clear();

        if (narrow)
        {
            HeaderGrid.ColumnDefinitions.Add(StarColumn());
            HeaderGrid.RowDefinitions.Add(AutoRow());
            HeaderGrid.RowDefinitions.Add(AutoRow());
            HeaderGrid.RowDefinitions.Add(AutoRow());
            Grid.SetColumn(ProfileCard, 0);
            Grid.SetRow(ProfileCard, 1);
            Grid.SetColumn(RefreshButton, 0);
            Grid.SetRow(RefreshButton, 2);

            MetricsGrid.ColumnDefinitions.Add(StarColumn());
            for (var i = 0; i < 4; i++)
            {
                MetricsGrid.RowDefinitions.Add(AutoRow());
            }
            SetMetricPosition(DocumentsMetric, 0, 0);
            SetMetricPosition(QueriesMetric, 0, 1);
            SetMetricPosition(ReadyMetric, 0, 2);
            SetMetricPosition(MineMetric, 0, 3);

            ContentGrid.ColumnDefinitions.Add(StarColumn());
            ContentGrid.RowDefinitions.Add(AutoRow());
            ContentGrid.RowDefinitions.Add(AutoRow());
            Grid.SetColumn(RecentDocumentsPanel, 0);
            Grid.SetRow(RecentDocumentsPanel, 0);
            Grid.SetColumn(RecentQuestionsPanel, 0);
            Grid.SetRow(RecentQuestionsPanel, 1);
            return;
        }

        HeaderGrid.ColumnDefinitions.Add(StarColumn());
        HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        HeaderGrid.RowDefinitions.Add(AutoRow());
        Grid.SetColumn(ProfileCard, 1);
        Grid.SetRow(ProfileCard, 0);
        Grid.SetColumn(RefreshButton, 2);
        Grid.SetRow(RefreshButton, 0);

        for (var i = 0; i < 4; i++)
        {
            MetricsGrid.ColumnDefinitions.Add(StarColumn());
        }
        SetMetricPosition(DocumentsMetric, 0, 0);
        SetMetricPosition(QueriesMetric, 1, 0);
        SetMetricPosition(ReadyMetric, 2, 0);
        SetMetricPosition(MineMetric, 3, 0);

        ContentGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) }
        );
        ContentGrid.ColumnDefinitions.Add(StarColumn());
        ContentGrid.RowDefinitions.Add(AutoRow());
        Grid.SetColumn(RecentDocumentsPanel, 0);
        Grid.SetRow(RecentDocumentsPanel, 0);
        Grid.SetColumn(RecentQuestionsPanel, 1);
        Grid.SetRow(RecentQuestionsPanel, 0);
    }

    private static ColumnDefinition StarColumn() =>
        new() { Width = new GridLength(1, GridUnitType.Star) };

    private static RowDefinition AutoRow() => new() { Height = GridLength.Auto };

    private static void SetMetricPosition(FrameworkElement metric, int column, int row)
    {
        Grid.SetColumn(metric, column);
        Grid.SetRow(metric, row);
    }
}
