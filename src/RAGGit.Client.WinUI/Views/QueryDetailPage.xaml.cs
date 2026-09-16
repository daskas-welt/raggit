using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class QueryDetailPage : Page
{
    public QueryDetailViewModel ViewModel { get; }

    public QueryDetailPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<QueryDetailViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            ShowDetail(id);
        }
    }

    public void ShowDetail(Guid id) => ViewModel.LoadDetailCommand.Execute(id);

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
