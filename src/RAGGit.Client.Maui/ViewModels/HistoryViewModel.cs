using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the per-person history view (005-per-person-history US1).
/// First page on load, LoadMore appends by offset, refresh resets, empty
/// state when the person has no queries.
/// </summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly QueryHistoryApiClient _apiClient;

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

    public HistoryViewModel(QueryHistoryApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
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
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
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
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
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
