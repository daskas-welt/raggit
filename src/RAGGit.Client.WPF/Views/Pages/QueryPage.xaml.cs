using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class QueryPage : Page
{
    public QueryViewModel ViewModel { get; }

    public QueryPage()
    {
        ViewModel = App.Services.GetRequiredService<QueryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
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
        };
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
