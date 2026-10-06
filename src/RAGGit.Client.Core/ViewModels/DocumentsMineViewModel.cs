using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// ViewModel for the per-person recent-documents view (005-per-person-history US3).
/// First page on load, LoadMore appends by offset, refresh resets, empty
/// state when the person uploaded nothing. Mirrors <see cref="HistoryViewModel"/>.
/// </summary>
public sealed partial class DocumentsMineViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan DefaultStatusPollInterval = TimeSpan.FromSeconds(2);

    private readonly DocumentsApiClient _apiClient;
    private readonly SearchSessionState? _searchSession;
    private readonly DocumentStatusPoller _statusPoller;

    [ObservableProperty]
    private ObservableCollection<DocumentMineItem> _items = new();

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private int _limit = 20;

    [ObservableProperty]
    private int _offset;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _hasMore;

    [ObservableProperty]
    private string? _errorMessage;

    public DocumentsMineViewModel(
        DocumentsApiClient apiClient,
        SearchSessionState? searchSession = null,
        TimeSpan? statusPollInterval = null
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        // Pages are transient, so the typed search lives in the session singleton
        // and is re-applied to whatever this instance loads (029, FR-003).
        _searchSession = searchSession;
        _searchText = searchSession?.Get(SearchSurface.MyDocuments) ?? string.Empty;
        _statusPoller = new DocumentStatusPoller(statusPollInterval ?? DefaultStatusPollInterval);
    }

    /// <summary>
    /// The current search text. Matching is a case-insensitive substring over
    /// the filename of the records loaded so far.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        _searchSession?.Set(SearchSurface.MyDocuments, value);
        NotifySearch();
    }

    private string SearchTerm => SearchText.Trim();

    /// <summary>True while a non-blank search is narrowing the loaded items.</summary>
    public bool IsSearchActive => SearchTerm.Length > 0;

    /// <summary>The loaded items whose filename contains the search text.</summary>
    public IReadOnlyList<DocumentMineItem> FilteredItems =>
        IsSearchActive
            ? Items
                .Where(i => i.Filename.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList()
            : Items;

    /// <summary>
    /// How many loaded items match. This is never the server total — only the
    /// loaded page is searchable, and "Load more" extends it.
    /// </summary>
    public int MatchCount => FilteredItems.Count;

    /// <summary>False only when an active search matches nothing loaded.</summary>
    public bool HasMatch => MatchCount > 0;

    /// <summary>
    /// Match caption for the incremental surface: counts loaded matches, never
    /// the server total, so it cannot imply a search it did not perform.
    /// </summary>
    public string MatchCaption => $"{MatchCount} matching of {Items.Count} loaded";

    private void NotifySearch()
    {
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(MatchCount));
        OnPropertyChanged(nameof(HasMatch));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(MatchCaption));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadPageAsync(0, append: false);
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsBusy)
        {
            return;
        }

        // Append after everything already loaded, not after the last page
        // boundary: the status poll refetches from offset 0 and rewrites
        // Offset, so Offset + Limit would re-request a page already in Items.
        await LoadPageAsync(Items.Count, append: true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPageAsync(0, append: false);
    }

    private async Task LoadPageAsync(int offset, bool append)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var page = await _apiClient.GetMineAsync(Limit, offset);
            Limit = page.Limit;
            Offset = page.Offset;
            Total = page.Total;

            if (append)
            {
                foreach (var item in page.Items)
                {
                    Items.Add(item);
                }
            }
            else
            {
                Items = new ObservableCollection<DocumentMineItem>(page.Items);
            }

            IsEmpty = Total == 0;
            HasMore = Items.Count < Total;
            // The search survives the reload and re-applies to the fresh data,
            // so a refresh never flashes the unfiltered list.
            NotifySearch();
            StartStatusPolling();
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "cannot reach AI workstation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ClientErrorText.CannotReach(ex.Message);
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "AI workstation unavailable",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = ClientErrorText.Unavailable(ex.Message);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Failed to load documents: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StartStatusPolling() =>
        _statusPoller.Start(
            keepPolling: () => Items.Any(IsActiveStatus),
            pollOnce: PollStatusesAsync,
            onError: exception =>
                ErrorMessage = $"Could not refresh document status: {exception.Message}"
        );

    private async Task PollStatusesAsync(CancellationToken cancellationToken)
    {
        // One refetch of the whole loaded window, clamped to the endpoint's
        // 100-row cap. Rows appended past that cap refresh on the next load.
        var loaded = Math.Max(Items.Count, Limit);
        var page = await _apiClient.GetMineAsync(loaded, 0, cancellationToken);

        if (!StatusSetChanged(page.Items))
        {
            return;
        }

        Items = new ObservableCollection<DocumentMineItem>(page.Items);
        Offset = page.Offset;
        Total = page.Total;
        IsEmpty = Total == 0;
        HasMore = Items.Count < Total;
        NotifySearch();
    }

    private bool StatusSetChanged(IReadOnlyList<DocumentMineItem> incoming)
    {
        if (incoming.Count != Items.Count)
        {
            return true;
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            if (incoming[i].Id != Items[i].Id || incoming[i].Status != Items[i].Status)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveStatus(DocumentMineItem item) =>
        Enum.TryParse<DocumentStatus>(item.Status, ignoreCase: true, out var status)
        && status is DocumentStatus.Uploading or DocumentStatus.Queued or DocumentStatus.Indexing;

    /// <summary>
    /// Stops the status loop. The page calls this on unload so a navigated-away
    /// view never keeps polling the workstation.
    /// </summary>
    public void StopStatusPolling() => _statusPoller.Stop();

    public void Dispose() => _statusPoller.Dispose();
}
