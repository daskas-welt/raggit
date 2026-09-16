using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class AdminUsersPage : Page
{
    public AdminUsersViewModel ViewModel { get; }

    public AdminUsersPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<AdminUsersViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Users.Count == 0 && !ViewModel.IsBusy)
        {
            ViewModel.LoadUsersCommand.Execute(null);
        }
    }
}
