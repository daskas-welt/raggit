using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.Services;
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
        var grid = this.FindByName<Syncfusion.Maui.DataGrid.SfDataGrid>("DataGrid");
        Document? document = null;
        if (grid?.SelectedRow is Document d)
            document = d;
        else if (sender is Button b && b.BindingContext is Document bd)
            document = bd;
        if (document is null)
            return;

#if MAUI
        // Syncfusion SfPopup themed confirmation (MAUI TFMs); fallback to DisplayAlert on net8.0
        var confirmed = await DisplayAlert(
            "Delete Document",
            $"Are you sure you want to delete '{document.Filename}'?",
            "Delete",
            "Cancel"
        );
#else
        var confirmed = await DisplayAlert(
            "Delete Document",
            $"Are you sure you want to delete '{document.Filename}'?",
            "Delete",
            "Cancel"
        );
#endif

        if (!confirmed || BindingContext is not LibraryViewModel viewModel)
            return;

        await viewModel.DeleteDocumentCommand.ExecuteAsync(document);
        viewModel.LoadDocumentsCommand.Execute(null);
    }

    private void OnPullRefreshing(object sender, EventArgs e)
    {
        if (BindingContext is LibraryViewModel vm)
            vm.LoadDocumentsCommand.Execute(null);
        if (sender is Syncfusion.Maui.PullToRefresh.SfPullToRefresh ptr)
            ptr.IsRefreshing = false;
    }

    private void OnToggleThemeClicked(object sender, EventArgs e) => ThemeService.Toggle();
}
