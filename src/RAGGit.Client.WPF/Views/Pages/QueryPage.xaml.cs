using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    private CancellationTokenSource? _historyCts;

    public QueryPage()
    {
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ask-again arrivals land here: populate the input with the
        // original question (never auto-send). The read clears the
        // state, so later visits start empty.
        var state = App.Services.GetRequiredService<AskNavigationState>();
        var prompt = state.PendingPrompt;
        state.PendingPrompt = null;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            ViewModel.QueryText = prompt;
        }

        // A fresh process has an empty in-memory store, so seed the
        // conversation from the employee's recent queries. Cancelled on
        // navigation away; a cancelled load leaves the cache untouched.
        _historyCts?.Cancel();
        var cts = new CancellationTokenSource();
        _historyCts = cts;
        try
        {
            await ViewModel.EnsureConversationLoadedAsync(cts.Token);
        }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_historyCts, cts))
            {
                _historyCts = null;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Only cancel here: the load owns disposal once it unwinds.
        _historyCts?.Cancel();
    }

    private void OnClearConversationClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ClearConversationCommand.CanExecute(null))
        {
            ViewModel.ClearConversationCommand.Execute(null);
        }

        // U5.3: the emptied conversation leaves the input ready for the
        // next question.
        Chat.FocusInput();
    }
}
