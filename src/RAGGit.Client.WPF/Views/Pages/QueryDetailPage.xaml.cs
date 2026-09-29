using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
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
            // QueryViewModel.QueryText is settable, so preset the prompt when supported.
            // NOTE: pages/VMs are registered transient, so this best-effort preset only
            // takes effect if the QueryPage ends up sharing the instance.
            App.Services.GetRequiredService<QueryViewModel>().QueryText = prompt;
        }

        App.Services.GetRequiredService<INavigationService>().NavigateToAsk();
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e) =>
        CopyText(ViewModel.Detail?.Prompt);

    private void CopyAnswerButton_Click(object sender, RoutedEventArgs e) =>
        CopyText(ViewModel.Detail?.Answer);

    private void CopyCitationButton_Click(object sender, RoutedEventArgs e)
    {
        if (
            (sender as FrameworkElement)?.DataContext is HistoryCitation citation
            && !string.IsNullOrEmpty(citation.Text)
        )
        {
            CopyText($"Doc: {citation.DocumentId} / Chunk: {citation.ChunkId}\n{citation.Text}");
        }
    }

    private static void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard may be locked by another process; copy stays best-effort.
        }
    }
}
