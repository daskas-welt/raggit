using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// ViewModel for the per-person history view (005-per-person-history US1).
/// First page on load, LoadMore appends by offset, refresh resets, empty
/// state when the person has no queries.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly QueryHistoryApiClient _apiClient;
    private readonly SearchSessionState? _searchSession;

    [ObservableProperty]
    private ObservableCollection<HistoryItem> _items = new();

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

    public HistoryViewModel(
        QueryHistoryApiClient apiClient,
        SearchSessionState? searchSession = null
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        // Pages are transient, so the typed search lives in the session singleton
        // and is re-applied to whatever this instance loads (029, FR-003).
        _searchSession = searchSession;
        _searchText = searchSession?.Get(SearchSurface.History) ?? string.Empty;
    }

    /// <summary>
    /// The current search text. Matching is a case-insensitive substring over
    /// the question and answer text of the records loaded so far.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        _searchSession?.Set(SearchSurface.History, value);
        NotifySearch();
    }

    private string SearchTerm => SearchText.Trim();

    /// <summary>True while a non-blank search is narrowing the loaded items.</summary>
    public bool IsSearchActive => SearchTerm.Length > 0;

    /// <summary>The loaded items whose question or answer contains the search text.</summary>
    public IReadOnlyList<HistoryItem> FilteredItems =>
        IsSearchActive
            ? Items
                .Where(i =>
                    i.PromptPreview.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                    || i.AnswerPreview.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                )
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

        await LoadPageAsync(Offset + Limit, append: true);
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
            var page = await _apiClient.GetHistoryAsync(Limit, offset);
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
                Items = new ObservableCollection<HistoryItem>(page.Items);
            }

            IsEmpty = Total == 0;
            HasMore = Items.Count < Total;
            // The search survives the reload and re-applies to the fresh data,
            // so a refresh never flashes the unfiltered list.
            NotifySearch();
        }
        catch (HttpRequestException ex)
            when (ex.Message.Contains(
                    "model unavailable offline",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            ErrorMessage = "model unavailable offline";
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
            ErrorMessage = $"Failed to load history: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
