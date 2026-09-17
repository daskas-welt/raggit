using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;

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

    private async void OnChangeRoleClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not UserAccountDto user)
        {
            return;
        }

        var newRole = user.Role == UserRole.Admin ? UserRole.Employee : UserRole.Admin;
        var confirm = new ContentDialog
        {
            Title = "Change Role",
            Content = $"Change '{user.Username}' from {user.Role} to {newRole}?",
            PrimaryButtonText = "Change Role",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ChangeRoleCommand.ExecuteAsync(user);
        }
    }

    private async void OnToggleActiveClicked(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not UserAccountDto user)
        {
            return;
        }

        var verb = user.IsActive ? "Deactivate" : "Activate";
        var confirm = new ContentDialog
        {
            Title = $"{verb} User",
            Content = $"Are you sure you want to {verb.ToLowerInvariant()} '{user.Username}'?",
            PrimaryButtonText = verb,
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
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

        var confirm = new ContentDialog
        {
            Title = "Reset Password",
            Content = $"Reset the password for '{user.Username}'?",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ResetPasswordCommand.ExecuteAsync(user);
        }
    }
}
