using System;
using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.Maui.Views;

public partial class QueryDetailView : ContentPage
{
    public QueryDetailView(QueryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>
    /// Loads the full answer + citations for one history entry.
    /// Called by the history list when the person taps an entry.
    /// </summary>
    public void ShowDetail(Guid id)
    {
        if (BindingContext is QueryDetailViewModel viewModel)
            viewModel.LoadDetailCommand.Execute(id);
    }
}
