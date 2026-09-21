using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    public QueryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
    }

    /// <summary>
    /// Re-runs a prompt passed from query history: navigate with
    /// <c>Frame.Navigate(typeof(QueryPage), prompt)</c>.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            _ = ViewModel.ReaskAsync(prompt);
        }
    }
}
