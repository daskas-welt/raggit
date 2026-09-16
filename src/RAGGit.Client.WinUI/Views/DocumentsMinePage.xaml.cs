using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class DocumentsMinePage : Page
{
    public DocumentsMineViewModel ViewModel { get; }

    public DocumentsMinePage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<DocumentsMineViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Items.Count == 0 && !ViewModel.IsBusy)
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }
}
