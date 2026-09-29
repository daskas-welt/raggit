using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Maui.ViewModels;

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
}
