using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class AdminUsersPage : Page
{
    public AdminUsersViewModel ViewModel { get; }

    public AdminUsersPage()
    {
        ViewModel = App.Services.GetRequiredService<AdminUsersViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (ViewModel.Users.Count == 0 && !ViewModel.IsBusy)
            {
                _ = ViewModel.LoadUsersCommand.ExecuteAsync(null);
            }
        };
    }

    private async void OnChangeRoleClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserAccountDto user)
        {
            return;
        }

        var newRole = user.Role == UserRole.Admin ? UserRole.Employee : UserRole.Admin;
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            await dialogs.ConfirmAsync(
                "Change Role",
                $"Change '{user.Username}' from {user.Role} to {newRole}?"
            )
        )
        {
            await ViewModel.ChangeRoleCommand.ExecuteAsync(user);
        }
    }

    private async void OnToggleActiveClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserAccountDto user)
        {
            return;
        }

        var verb = user.IsActive ? "Deactivate" : "Activate";
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            await dialogs.ConfirmAsync(
                $"{verb} User",
                $"Are you sure you want to {verb.ToLowerInvariant()} '{user.Username}'?"
            )
        )
        {
            await ViewModel.ToggleActiveCommand.ExecuteAsync(user);
        }
    }

    private async void OnResetPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedUser is not UserAccountDto user)
        {
            // Preserve the ViewModel's "Select a user first." validation.
            await ViewModel.ResetPasswordCommand.ExecuteAsync(null);
            return;
        }

        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            await dialogs.ConfirmAsync(
                "Reset Password",
                $"Reset the password for '{user.Username}'?"
            )
        )
        {
            await ViewModel.ResetPasswordCommand.ExecuteAsync(user);
        }
    }
}
