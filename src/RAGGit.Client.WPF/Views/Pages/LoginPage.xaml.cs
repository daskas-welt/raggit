using System;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.ViewModels;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // PasswordBox.Password is not a DependencyProperty and cannot be data-bound.
    // Sync it to the ViewModel explicitly on every change.
    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ViewModel.Password != PasswordBox.Password)
        {
            ViewModel.Password = PasswordBox.Password;
        }
    }

    // Enter in either credential box submits sign-in when no sign-in is in flight.
    private void OnCredentialKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ViewModel.IsBusy)
        {
            return;
        }

        // Same manual sync care as OnPasswordChanged: the box is the source of truth.
        if (!string.Equals(ViewModel.Password, PasswordBox.Password, StringComparison.Ordinal))
        {
            ViewModel.Password = PasswordBox.Password;
        }

        if (ViewModel.LoginCommand.CanExecute(null))
        {
            ViewModel.LoginCommand.Execute(null);
            e.Handled = true;
        }
    }
}
