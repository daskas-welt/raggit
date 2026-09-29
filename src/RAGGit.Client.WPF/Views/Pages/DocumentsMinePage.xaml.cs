using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class DocumentsMinePage : Page
{
    public DocumentsMineViewModel ViewModel { get; }

    public DocumentsMinePage()
    {
        ViewModel = App.Services.GetRequiredService<DocumentsMineViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel.Items.Count == 0 && !ViewModel.IsBusy)
            {
                _ = ViewModel.LoadCommand.ExecuteAsync(null);
            }
        };
    }
}
