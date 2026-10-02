using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Client.WPF.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class QueryDetailPage : Page
{
    public QueryDetailViewModel ViewModel { get; }

    public QueryDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<QueryDetailViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var state = App.Services.GetRequiredService<QueryDetailNavigationState>();
            if (state.PendingQueryId is { } queryId)
            {
                state.PendingQueryId = null;
                _ = ViewModel.LoadDetailCommand.ExecuteAsync(queryId);
            }
        };
    }

    private void OnBackClicked(object sender, RoutedEventArgs e) =>
        App.Services.GetRequiredService<INavigationService>().GoBack();

    private void OnAskAgainClicked(object sender, RoutedEventArgs e)
    {
        var prompt = ViewModel.Detail?.Prompt;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            // Pages are transient, so the prompt travels in the shared
            // pending-ask state; QueryPage picks it up on load.
            App.Services.GetRequiredService<AskNavigationState>().PendingPrompt = prompt;
        }

        App.Services.GetRequiredService<INavigationService>().NavigateToAsk();
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e) =>
        CopyText(ViewModel.Detail?.Prompt, "Prompt");

    private void CopyAnswerButton_Click(object sender, RoutedEventArgs e) =>
        CopyText(ViewModel.Detail?.Answer, "Answer");

    private void CopyCitationButton_Click(object sender, RoutedEventArgs e)
    {
        if (
            (sender as FrameworkElement)?.DataContext is HistoryCitation citation
            && !string.IsNullOrEmpty(citation.Text)
        )
        {
            CopyText(
                $"Doc: {citation.DocumentId} / Chunk: {citation.ChunkId}\n{citation.Text}",
                "Citation"
            );
        }
    }

    private void CopyText(string? text, string what)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            App.Services.GetRequiredService<INotificationService>()
                .Show("Copied", $"{what} copied to clipboard.", NotificationKind.Success);
        }
        catch
        {
            // Clipboard may be locked by another process; copy stays best-effort.
        }
    }
}
