using System.Windows;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Dialogs;

public partial class CreatePersonDialog : Window
{
    public AdminUsersViewModel ViewModel { get; }

    public CreatePersonDialog(AdminUsersViewModel viewModel)
    {
        ViewModel = viewModel;
        ViewModel.ClearStatus();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void OnCreateClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.CreateUserCommand.ExecuteAsync(null);
        if (ViewModel.StatusSeverity == "Success")
        {
            DialogResult = true;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}
