using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the employee query view.
/// </summary>
public sealed partial class QueryViewModel : ObservableObject
{
    private readonly QueryApiClient _apiClient;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private string _answer = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Citation> _citations = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAskEnabled = true;

    [ObservableProperty]
    private bool _isResultVisible;

    [ObservableProperty]
    private bool _hasCitations;

    [ObservableProperty]
    private string? _statusMessage;

    public QueryViewModel(QueryApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync()
    {
        if (string.IsNullOrWhiteSpace(QueryText))
        {
            StatusMessage = "Please enter a question.";
            return;
        }

        IsBusy = true;
        IsAskEnabled = false;
        IsResultVisible = false;
        StatusMessage = null;
        Answer = string.Empty;
        Citations = new ObservableCollection<Citation>();
        HasCitations = false;

        try
        {
            var response = await _apiClient.QueryAsync(QueryText);
            Answer = response.Answer;
            Citations = new ObservableCollection<Citation>(response.Citations);
            HasCitations = response.Citations.Count > 0;
            IsResultVisible = true;

            if (response.Citations.Count == 0)
            {
                StatusMessage = $"Answered in {response.LatencyMs}ms (no citations).";
            }
            else
            {
                StatusMessage = $"Answered in {response.LatencyMs}ms with {response.Citations.Count} citation(s).";
            }
        }
        catch (HttpRequestException exception)
        {
            StatusMessage = $"AI workstation unavailable: {exception.Message}";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Query failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            IsAskEnabled = true;
        }
    }

    private bool CanAsk => IsAskEnabled && !string.IsNullOrWhiteSpace(QueryText);
}
