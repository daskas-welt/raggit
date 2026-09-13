using System;
using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.Maui.Views;

public partial class DocumentsMineView : ContentPage
{
    public DocumentsMineView(DocumentsMineViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (
            BindingContext is DocumentsMineViewModel viewModel
            && viewModel.Items.Count == 0
            && !viewModel.IsBusy
        )
            viewModel.LoadCommand.Execute(null);
    }

    private void OnLoadMore(object? sender, EventArgs e)
    {
        if (BindingContext is DocumentsMineViewModel viewModel)
            viewModel.LoadMoreCommand.Execute(null);
    }

    private void OnPullRefreshing(object? sender, EventArgs e)
    {
        if (BindingContext is DocumentsMineViewModel viewModel)
            viewModel.RefreshCommand.Execute(null);
        if (sender is Syncfusion.Maui.PullToRefresh.SfPullToRefresh pullToRefresh)
            pullToRefresh.IsRefreshing = false;
    }
}
