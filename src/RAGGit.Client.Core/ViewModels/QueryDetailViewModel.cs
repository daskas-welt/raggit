using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// ViewModel for the per-person query detail view (005-per-person-history US2).
/// Loads one owned query (full answer + ordered citations) via
/// <see cref="QueryHistoryApiClient.GetDetailAsync"/>; non-owned ids surface
/// the 404 as an error message instead of another person's data.
/// </summary>
public sealed partial class QueryDetailViewModel : ObservableObject
{
    private readonly QueryHistoryApiClient _apiClient;

    [ObservableProperty]
    private QueryDetail? _detail;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public QueryDetailViewModel(QueryHistoryApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    [RelayCommand]
    private async Task LoadDetailAsync(Guid id)
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            Detail = await _apiClient.GetDetailAsync(id);
        }
        catch (HttpRequestException ex)
        {
            Detail = null;
            ErrorMessage = ex.Message;
        }
        catch (Exception exception)
        {
            Detail = null;
            ErrorMessage = $"Failed to load query detail: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
