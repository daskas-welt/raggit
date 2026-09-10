using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.Maui.Views;

public partial class LibraryView : ContentPage
{
    public LibraryView(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
