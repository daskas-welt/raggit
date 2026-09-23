using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using RAGGit.Client.Maui.ViewModels;

namespace RAGGit.Client.WinUI.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }

    public LoginPage()
    {
        InitializeComponent();

        var auth = App.Services.GetRequiredService<RAGGit.Client.Maui.Services.AuthApiClient>();
        ViewModel = new LoginViewModel(
            auth,
            App.Session,
            async () =>
            {
                var window = App.MainWindow;
                if (window is not null)
                {
                    await window.DiscoverRoleAsync();
                    window.NavigateToDashboard();
                }
            }
        );
    }
}
