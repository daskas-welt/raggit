using System;
using RAGGit.Client.Core.Services;
using RAGGit.Client.WPF.Views.Pages;

namespace RAGGit.Client.WPF.Services;

/// <summary>
/// Adapter from the shared <see cref="RAGGit.Client.Core.Services.INavigationService"/>
/// to WPF-UI's <see cref="Wpf.Ui.INavigationService"/>.
/// </summary>
public sealed class WpfNavigationService : RAGGit.Client.Core.Services.INavigationService
{
    private readonly Wpf.Ui.INavigationService _navigationService;
    private readonly QueryDetailNavigationState _detailState;

    public WpfNavigationService(
        Wpf.Ui.INavigationService navigationService,
        QueryDetailNavigationState detailState
    )
    {
        _navigationService = navigationService;
        _detailState = detailState;
    }

    public void NavigateToDashboard() => _navigationService.Navigate(typeof(DashboardPage));

    public void NavigateToLibrary() => _navigationService.Navigate(typeof(LibraryPage));

    public void NavigateToAsk() => _navigationService.Navigate(typeof(QueryPage));

    public void NavigateToHistory() => _navigationService.Navigate(typeof(HistoryPage));

    public void NavigateToMyDocs() => _navigationService.Navigate(typeof(DocumentsMinePage));

    public void NavigateToAdmin() => _navigationService.Navigate(typeof(AdminUsersPage));

    public void NavigateToLogin() => _navigationService.Navigate(typeof(LoginPage));

    public void NavigateToQueryDetail(Guid queryId)
    {
        _detailState.PendingQueryId = queryId;
        _navigationService.Navigate(typeof(QueryDetailPage));
    }

    public void GoBack() => _navigationService.GoBack();
}
