using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.Views;

public partial class LibraryView : ContentPage
{
    public LibraryView(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.BindingContext is not Document document)
        {
            return;
        }

        var confirmed = await DisplayAlert(
            "Delete Document",
            $"Are you sure you want to delete '{document.Filename}'?",
            "Delete",
            "Cancel");

        if (!confirmed || BindingContext is not LibraryViewModel viewModel)
        {
            return;
        }

        await viewModel.DeleteDocumentCommand.ExecuteAsync(document);
        viewModel.LoadDocumentsCommand.Execute(null);
    }
}
