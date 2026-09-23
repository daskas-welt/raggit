using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    private CancellationTokenSource? _historyCts;

    public QueryPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
    }

    /// <summary>
    /// Loads the current login session's conversation on every visit (a
    /// restart or a new login starts empty by design; the full archive lives
    /// on the History page), then re-runs a prompt passed from query history:
    /// navigate with <c>Frame.Navigate(typeof(QueryPage), prompt)</c>.
    /// Aborted loads (navigation away) leave the cached messages untouched.
    /// </summary>
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _historyCts?.Cancel();
        _historyCts?.Dispose();
        _historyCts = new CancellationTokenSource();
        await ViewModel.LoadHistoryAsync(_historyCts.Token);
        if (e.Parameter is string prompt && !string.IsNullOrWhiteSpace(prompt))
        {
            await ViewModel.ReaskAsync(prompt);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _historyCts?.Cancel();
    }
}
