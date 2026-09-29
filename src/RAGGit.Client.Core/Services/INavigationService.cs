using System;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// UI-framework-agnostic navigation service. The WPF client provides an
/// implementation that wraps WPF-UI's <c>INavigationService</c>; the WinUI
/// client is being replaced, but the interface stays in the shared core so
/// ViewModels remain testable and framework-free.
/// </summary>
public interface INavigationService
{
    void NavigateToDashboard();
    void NavigateToLibrary();
    void NavigateToAsk();
    void NavigateToHistory();
    void NavigateToMyDocs();
    void NavigateToAdmin();
    void NavigateToLogin();
    void NavigateToQueryDetail(Guid queryId);
    void GoBack();
}

/// <summary>
/// UI-framework-agnostic dialog service for confirmations and alerts.
/// </summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
}
