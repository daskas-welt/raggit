using System.Windows;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Dialogs;

public partial class ResetPasswordDialog : Window
{
    public AdminUsersViewModel ViewModel { get; }

    public UserAccountDto TargetUser { get; }

    public ResetPasswordDialog(AdminUsersViewModel viewModel, UserAccountDto targetUser)
    {
        ViewModel = viewModel;
        TargetUser = targetUser;
        ViewModel.ClearStatus();
        ViewModel.ResetPassword = string.Empty;
        ViewModel.ResetMustChangePassword = false;
        ViewModel.PasswordResetCompleted = false;
        DataContext = this;
        InitializeComponent();
    }

    private async void OnResetClicked(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            $"Reset the password for '{TargetUser.Username}'?",
            "Reset password",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await ViewModel.ResetPasswordCommand.ExecuteAsync(TargetUser);
        if (ViewModel.PasswordResetCompleted)
        {
            DialogResult = true;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}
