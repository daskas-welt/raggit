using Microsoft.Maui.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.Maui.Views;

public partial class UploadView : ContentPage
{
    public UploadView(UploadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
