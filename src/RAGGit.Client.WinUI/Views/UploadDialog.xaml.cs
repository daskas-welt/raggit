using Microsoft.UI.Xaml.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

/// <summary>
/// Upload form as a dialog (the MAUI right-anchored sheet becomes a
/// ContentDialog on desktop; same ViewModel, same cancel semantics).
/// </summary>
public sealed partial class UploadDialog : ContentDialog
{
    public UploadViewModel ViewModel { get; }

    public UploadDialog(UploadViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
