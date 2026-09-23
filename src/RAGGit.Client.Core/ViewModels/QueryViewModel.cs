using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Models;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the employee query view.
/// </summary>
public sealed partial class QueryViewModel : ObservableObject
{
    /// <summary>
    /// Upper bound on session queries materialized into the Ask chat (each
    /// yields a user + assistant message). History list rows are previews, so
    /// every item costs one detail round-trip; the cap keeps navigation
    /// snappy while the full archive stays on the History page.
    /// </summary>
    public const int MaxSessionItems = 50;

    private readonly QueryApiClient _apiClient;
    private readonly QueryHistoryApiClient _historyApiClient;
    private readonly ClientSession _session;
    private readonly ConversationStore _conversation;

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
    private ObservableCollection<PersonSuggestion> _suggestedPersons = new();

    [ObservableProperty]
    private bool _hasSuggestedPersons;

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

    partial void OnIsAskEnabledChanged(bool value)
    {
        AskCommand.NotifyCanExecuteChanged();
        UseSuggestedPersonCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        AskCommand.NotifyCanExecuteChanged();
        UseSuggestedPersonCommand.NotifyCanExecuteChanged();
    }

    public QueryViewModel(
        QueryApiClient apiClient,
        QueryHistoryApiClient historyApiClient,
        ClientSession session,
        ConversationStore conversation
    )
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _historyApiClient =
            historyApiClient ?? throw new ArgumentNullException(nameof(historyApiClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));

        // The page is recreated on every navigation: re-seed from the
        // login-scoped cache so asked questions survive leaving Ask.
        _conversation.EnsureOwner(ConversationOwnerKey(_session));
        _messages = new ObservableCollection<ChatMessage>(_conversation.Messages);
    }

    internal static string ConversationOwnerKey(ClientSession session) =>
        $"{session.Username}|{session.LastLoginAtUtc:O}";

    /// <summary>
    /// Loads the current login session's queries (server scopes by JWT sub,
    /// client keeps only items at/after <see cref="ClientSession.LastLoginAtUtc"/>)
    /// as chat pairs, oldest first. Session-scoped by design: a restart or a
    /// new login starts with an empty conversation; the full archive lives on
    /// the History page. Bounded to the <see cref="MaxSessionItems"/> most
    /// recent in-session queries with details fetched in small parallel
    /// batches; pass a <see cref="CancellationToken"/> to abort on navigation
    /// away (aborted loads leave the cached messages and store untouched).
    /// Replaces <see cref="Messages"/> wholesale so user switches never leak
    /// across logins. History list rows carry 120-char previews, so each
    /// item's full detail is fetched for prompt/answer/citations.
    /// </summary>
    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            // History pages arrive newest-first: stop paging once items predate
            // the login boundary, and never hold more than the cap.
            var since = _session.LastLoginAtUtc;
            var items = new List<HistoryItem>();
            const int pageSize = 100;
            var offset = 0;
            while (items.Count < MaxSessionItems)
            {
                var page = await _historyApiClient.GetHistoryAsync(
                    pageSize,
                    offset,
                    cancellationToken
                );
                items.AddRange(page.Items);
                if (
                    items.Count >= page.Total
                    || page.Items.Count == 0
                    // Pages arrive newest-first: once any item predates the
                    // login boundary, every later page is older too.
                    || (since is not null && page.Items.Any(i => i.CreatedAt < since))
                )
                {
                    break;
                }

                offset += page.Items.Count;
            }

            var targets = items
                .Where(i => since is null || i.CreatedAt >= since)
                .OrderByDescending(i => i.CreatedAt)
                .ThenByDescending(i => i.Id)
                .Take(MaxSessionItems)
                .OrderBy(i => i.CreatedAt)
                .ThenBy(i => i.Id)
                .ToList();

            // Bounded parallel detail fetch; index-aligned so chat order is stable.
            var details = new QueryDetail[targets.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, targets.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken,
                },
                async (index, token) =>
                {
                    details[index] = await _historyApiClient.GetDetailAsync(
                        targets[index].Id,
                        token
                    );
                }
            );

            var messages = new ObservableCollection<ChatMessage>();
            foreach (var detail in details)
            {
                messages.Add(
                    new ChatMessage
                    {
                        Text = detail.Prompt,
                        IsUser = true,
                        Timestamp = detail.CreatedAt,
                    }
                );
                messages.Add(
                    new ChatMessage
                    {
                        Text = detail.Answer,
                        IsUser = false,
                        Timestamp = detail.CreatedAt,
                        Citations = detail
                            .Citations.OrderBy(c => c.Ordinal)
                            .Select(c => new Citation
                            {
                                DocumentId = c.DocumentId,
                                DocumentName = c.DocumentName,
                                ChunkId = c.ChunkId,
                                Text = c.Text,
                                Ordinal = c.Ordinal,
                            })
                            .ToList(),
                    }
                );
            }

            Messages = messages;
            _conversation.ReplaceAll(messages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navigated away mid-load: keep the cached messages and store untouched.
        }
        catch (HttpRequestException) when (cancellationToken.IsCancellationRequested)
        {
            // Same, but the client wrapped the cancellation in HttpRequestException.
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
            when (exception.Message.Contains(
                    "AI workstation unavailable",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            StatusMessage = exception.Message;
        }
        catch (HttpRequestException exception)
            when (exception.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Please sign in to see your conversation.";
        }
        catch (HttpRequestException exception)
        {
            StatusMessage = ClientErrorText.Unavailable(exception.Message);
        }
        catch (TaskCanceledException exception)
        {
            StatusMessage = ClientErrorText.Unavailable(exception.Message);
        }
        catch (Exception exception)
        {
            StatusMessage = $"Failed to load conversation: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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

        await AskQuestionAsync(question);
    }

    /// <summary>
    /// Re-queries using a did-you-mean person chip. Preserves the original
    /// intent by swapping the trailing name in the last user question with
    /// the suggested name; falls back to a person-details template.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUseSuggestion))]
    private async Task UseSuggestedPersonAsync(PersonSuggestion? suggestion)
    {
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.Name))
        {
            return;
        }

        var followUp = BuildFollowUpQuestion(suggestion.Name);
        QueryText = followUp;
        AskCommand.NotifyCanExecuteChanged();
        await AskQuestionAsync(followUp);
    }

    private bool CanUseSuggestion(PersonSuggestion? suggestion) =>
        IsAskEnabled
        && !IsBusy
        && suggestion is not null
        && !string.IsNullOrWhiteSpace(suggestion.Name);

    public string BuildFollowUpQuestion(string suggestedName)
    {
        var lastUser = Messages.LastOrDefault(m => m.IsUser)?.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(lastUser))
        {
            // Swap the trailing name after της/του/τον/την/για with the suggestion.
            var swapped = System.Text.RegularExpressions.Regex.Replace(
                lastUser,
                @"(της|του|τον|την|για)\s+[\p{L}\s]+$",
                m => $"{m.Groups[1].Value} {suggestedName}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            if (!string.Equals(swapped, lastUser, StringComparison.Ordinal))
            {
                return swapped;
            }

            return $"{lastUser} ({suggestedName})";
        }

        return ContainsGreek(suggestedName)
            ? $"Δώσε το συνολικό χρόνο προϋπηρεσίας εντός δημοσίου τομέα για: {suggestedName}"
            : $"Give details for: {suggestedName}";
    }

    private static bool ContainsGreek(string value)
    {
        foreach (var c in value)
        {
            if (c is >= '\u0370' and <= '\u03FF' or >= '\u1F00' and <= '\u1FFF')
            {
                return true;
            }
        }

        return false;
    }

    private async Task AskQuestionAsync(string question)
    {
        if (IsBusy)
        {
            return;
        }

        QueryText = string.Empty;

        IsBusy = true;
        IsAskEnabled = false;
        IsResultVisible = false;
        StatusMessage = null;
        Answer = string.Empty;
        Citations = new ObservableCollection<Citation>();
        HasCitations = false;
        SuggestedPersons = new ObservableCollection<PersonSuggestion>();
        HasSuggestedPersons = false;

        // Push user message to chat, then clear the input.
        var userMessage = new ChatMessage { Text = question, IsUser = true };
        Messages.Add(userMessage);
        _conversation.Messages.Add(userMessage);
        QueryText = string.Empty;

        try
        {
            var response = await _apiClient.QueryAsync(question);
            Answer = response.Answer;
            Citations = new ObservableCollection<Citation>(response.Citations);
            HasCitations = response.Citations.Count > 0;
            SuggestedPersons = new ObservableCollection<PersonSuggestion>(
                response.SuggestedPersons ?? new List<PersonSuggestion>()
            );
            HasSuggestedPersons = SuggestedPersons.Count > 0;
            IsResultVisible = true;
            // Push assistant message to chat
            var assistantMessage = new ChatMessage
            {
                Text = response.Answer,
                IsUser = false,
                Citations = response.Citations,
            };
            Messages.Add(assistantMessage);
            _conversation.Messages.Add(assistantMessage);

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

    /// <summary>
    /// Re-runs a prompt from query history (or any caller). Sets the input
    /// to <paramref name="prompt"/> and asks it as a fresh question.
    /// </summary>
    public async Task ReaskAsync(string? prompt)
    {
        if (!IsAskEnabled || IsBusy)
        {
            return;
        }

        var question = prompt?.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            StatusMessage = "Please enter a question.";
            return;
        }

        QueryText = question;
        AskCommand.NotifyCanExecuteChanged();
        await AskQuestionAsync(question);
    }

    private bool CanAsk => IsAskEnabled && !string.IsNullOrWhiteSpace(QueryText);
}
