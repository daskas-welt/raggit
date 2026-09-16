using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Models;
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

    // ChatView binding — unified conversation (user + assistant messages).
    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private ObservableCollection<string> _suggestions = new()
    {
        "Summarize with citations",
        "Show sources",
        "Try a broader query",
    };

    [ObservableProperty]
    private bool _hasStatusMessage;

    partial void OnStatusMessageChanged(string? value) =>
        HasStatusMessage = !string.IsNullOrWhiteSpace(value);

    partial void OnQueryTextChanged(string value) => AskCommand.NotifyCanExecuteChanged();

    partial void OnIsAskEnabledChanged(bool value) => AskCommand.NotifyCanExecuteChanged();

    public QueryViewModel(QueryApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    [RelayCommand(CanExecute = nameof(CanAsk))]
    private async Task AskAsync()
    {
        var question = QueryText.Trim();
        if (string.IsNullOrWhiteSpace(question))
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

        // Push user message to chat, then clear the input.
        Messages.Add(new ChatMessage { Text = question, IsUser = true });
        QueryText = string.Empty;

        try
        {
            var response = await _apiClient.QueryAsync(question);
            Answer = response.Answer;
            Citations = new ObservableCollection<Citation>(response.Citations);
            HasCitations = response.Citations.Count > 0;
            IsResultVisible = true;
            // Push assistant message to chat
            Messages.Add(new ChatMessage { Text = response.Answer, IsUser = false });

            if (response.Citations.Count == 0)
            {
                StatusMessage = $"Answered in {response.LatencyMs}ms (no citations).";
            }
            else
            {
                StatusMessage =
                    $"Answered in {response.LatencyMs}ms with {response.Citations.Count} citation(s).";
            }
        }
        catch (HttpRequestException exception)
            when (exception.Message.Contains(
                    "model unavailable offline",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            StatusMessage = "model unavailable offline";
        }
        catch (HttpRequestException exception)
            when (exception.Message.Contains(
                    "cannot reach AI workstation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            StatusMessage = ClientErrorText.CannotReach(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            if (
                exception.Message.Contains(
                    "AI workstation unavailable",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                StatusMessage = exception.Message;
            else if (exception.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
                StatusMessage = "Forbidden: you do not have permission.";
            else
                StatusMessage = ClientErrorText.Unavailable(exception.Message);
        }
        catch (TaskCanceledException exception)
        {
            StatusMessage = ClientErrorText.Unavailable(exception.Message);
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

    [RelayCommand]
    private async Task RetryAsync()
    {
        await AskAsync();
    }

    private bool CanAsk => IsAskEnabled && !string.IsNullOrWhiteSpace(QueryText);
}
