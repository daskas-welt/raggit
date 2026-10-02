using System;
using System.Windows.Controls;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;

namespace RAGGit.Client.WPF.Views.Dialogs;

/// <summary>
/// Reset-password form shown as in-window dialog content (feature 028): the
/// caller confirms first through <c>IDialogService</c>, then hosts this form
/// in a <c>ContentDialog</c> via the existing <c>IContentDialogService</c> host
/// and closes it on <see cref="RequestClose"/>. ViewModel and behavior are
/// unchanged from the former modal window, minus the native message box.
/// </summary>
public partial class ResetPasswordDialog : UserControl
{
    public AdminUsersViewModel ViewModel { get; }

    public UserAccountDto TargetUser { get; }

    /// <summary>Raised when the form wants its host dialog closed.</summary>
    public event EventHandler? RequestClose;

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
        Loaded += (_, _) => ResetPasswordBox.Focus();
    }

    private async void OnResetClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        await ViewModel.ResetPasswordCommand.ExecuteAsync(TargetUser);
        if (ViewModel.PasswordResetCompleted)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCancelClicked(object sender, System.Windows.RoutedEventArgs e) =>
        RequestClose?.Invoke(this, EventArgs.Empty);
}
