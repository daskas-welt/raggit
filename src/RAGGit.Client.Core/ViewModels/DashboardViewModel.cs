using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// Composes existing workstation projections for the authenticated dashboard.
/// It owns no data and deliberately exposes only aggregates supported by those projections.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly DocumentsApiClient _documentsClient;
    private readonly QueryHistoryApiClient _historyClient;
    private readonly ClientSession _session;

    [ObservableProperty]
    private ObservableCollection<Document> _recentDocuments = new();

    [ObservableProperty]
    private ObservableCollection<HistoryItem> _recentQueries = new();

    [ObservableProperty]
    private int _totalDocuments;

    [ObservableProperty]
    private int _readyDocuments;

    [ObservableProperty]
    private int _indexingDocuments;

    [ObservableProperty]
    private int _failedDocuments;

    [ObservableProperty]
    private int _totalQueries;

    [ObservableProperty]
    private int _myDocuments;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    private string _statusSeverity = "Informational";

    [ObservableProperty]
    private string? _errorMessage;

    public DashboardViewModel(
        DocumentsApiClient documentsClient,
        QueryHistoryApiClient historyClient,
        ClientSession session
    )
    {
        _documentsClient =
            documentsClient ?? throw new ArgumentNullException(nameof(documentsClient));
        _historyClient = historyClient ?? throw new ArgumentNullException(nameof(historyClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        IsAdmin = _session.IsAdmin;
    }

    public bool IsAdmin { get; private set; }

    public bool HasLibraryData => TotalDocuments > 0;

    public bool HasQueryData => TotalQueries > 0;

    public string RoleContext => IsAdmin ? "Administrator workspace" : "Employee workspace";

    public string UserDisplayName =>
        !string.IsNullOrWhiteSpace(_session.DisplayName) ? _session.DisplayName!
        : !string.IsNullOrWhiteSpace(_session.Username) ? _session.Username!
        : "Signed-in user";

    public string UserHandle =>
        !string.IsNullOrWhiteSpace(_session.Username)
            ? _session.Username!
            : "Account details unavailable";

    public string UserDetails => $"{RoleContext} | {_session.IdentityType}";

    public string LibrarySummary =>
        TotalDocuments == 0
            ? "No documents in the library"
            : $"{TotalDocuments} documents in the library";

    public string QuerySummary =>
        TotalQueries == 0 ? "No saved questions yet" : $"{TotalQueries} saved questions";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var documentsTask = _documentsClient.GetDocumentsAsync();
            var historyTask = _historyClient.GetHistoryAsync(limit: 5, offset: 0);
            var mineTask = _documentsClient.GetMineAsync(limit: 1, offset: 0);

            await Task.WhenAll(documentsTask, historyTask, mineTask);

            var documents = (await documentsTask).ToList();
            var history = await historyTask;
            var mine = await mineTask;

            TotalDocuments = documents.Count;
            ReadyDocuments = documents.Count(d => d.Status == DocumentStatus.Ready);
            IndexingDocuments = documents.Count(d =>
                d.Status
                    is DocumentStatus.Uploading
                        or DocumentStatus.Queued
                        or DocumentStatus.Indexing
            );
            FailedDocuments = documents.Count(d => d.Status == DocumentStatus.Failed);
            TotalQueries = history.Total;
            MyDocuments = mine.Total;

            RecentDocuments = new ObservableCollection<Document>(
                documents.OrderByDescending(d => d.CreatedAt).Take(5)
            );
            RecentQueries = new ObservableCollection<HistoryItem>(history.Items);
            NotifyDerivedProperties();
            SetStatus("Dashboard refreshed", isError: false);
        }
        catch (HttpRequestException ex)
        {
            SetStatus(MapRequestError(ex), isError: true);
        }
        catch (TaskCanceledException ex)
        {
            SetStatus(ClientErrorText.Unavailable(ex.Message), isError: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load dashboard: {ex.Message}", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    public void RefreshRole()
    {
        IsAdmin = _session.IsAdmin;
        OnPropertyChanged(nameof(UserDisplayName));
        OnPropertyChanged(nameof(UserHandle));
        OnPropertyChanged(nameof(UserDetails));
        OnPropertyChanged(nameof(RoleContext));
    }

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(HasLibraryData));
        OnPropertyChanged(nameof(HasQueryData));
        OnPropertyChanged(nameof(LibrarySummary));
        OnPropertyChanged(nameof(QuerySummary));
    }

    private void ClearStatus()
    {
        StatusMessage = null;
        ErrorMessage = null;
        HasStatus = false;

        // Deliberately does NOT reset StatusSeverity. It carries the outcome of the last
        // completed load, which the whole-dashboard empty state gates on
        // (DashboardPage.xaml, StatusSeverity == "Success"). Resetting it here blanked that
        // signal at the start of every load, so the empty state flickered off for the
        // duration of any refresh of an empty library. SetStatus always assigns a fresh
        // severity when the load lands, so the only consumer that sees the retained value
        // is the empty-state trigger — which is exactly what should stay stable.
        // StatusMessage/ErrorMessage/HasStatus still clear, so the InfoBar closes
        // immediately while a load is in flight.
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        ErrorMessage = isError ? message : null;
        HasStatus = true;
        StatusSeverity = isError ? "Error" : "Success";
    }

    private static string MapRequestError(HttpRequestException exception)
    {
        if (
            exception.Message.Contains(
                "model unavailable offline",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return "model unavailable offline";
        }

        if (
            exception.Message.Contains(
                "cannot reach AI workstation",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return ClientErrorText.CannotReach(exception.Message);
        }

        if (
            exception.Message.Contains(
                "AI workstation unavailable",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return exception.Message;
        }

        return ClientErrorText.Unavailable(exception.Message);
    }
}
