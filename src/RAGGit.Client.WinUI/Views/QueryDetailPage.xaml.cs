using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;
using Windows.ApplicationModel.DataTransfer;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class QueryDetailPage : Page
{
    public QueryDetailViewModel ViewModel { get; }

    public QueryDetailPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<QueryDetailViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is Guid id)
        {
            ShowDetail(id);
        }
    }

    public void ShowDetail(Guid id) => ViewModel.LoadDetailCommand.Execute(id);

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void OnAskAgainClicked(object sender, RoutedEventArgs e)
    {
        var prompt = ViewModel.Detail?.Prompt;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            Frame.Navigate(typeof(QueryPage), prompt);
        }
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e)
    {
        CopyText(ViewModel.Detail?.Prompt);
    }

    private void CopyAnswerButton_Click(object sender, RoutedEventArgs e)
    {
        CopyText(ViewModel.Detail?.Answer);
    }

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

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
