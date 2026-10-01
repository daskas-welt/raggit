using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.WPF.Services;
using RAGGit.Client.WPF.ViewModels;
using RAGGit.Client.WPF.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Views;

public sealed partial class MainWindow : FluentWindow
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        Wpf.Ui.INavigationService navigationService,
        IContentDialogService contentDialogService,
        ISnackbarService snackbarService,
        ClientSession session,
        IServiceProvider services
    )
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        var pageProvider = services.GetRequiredService<INavigationViewPageProvider>();
        RootNavigationView.SetPageProviderService(pageProvider);
        navigationService.SetNavigationControl(RootNavigationView);
        contentDialogService.SetDialogHost(RootContentDialog);
        snackbarService.SetSnackbarPresenter(RootSnackbarPresenter);

        WpfSessionExpiryNavigator.TryNavigateToLogin = () =>
        {
            if (Dispatcher.CheckAccess())
            {
                TryNavigateToLogin(session);
                return true;
            }

            Dispatcher.Invoke(() => TryNavigateToLogin(session));
            return true;
        };

        SystemThemeWatcher.Watch(this);

        Loaded += async (_, _) => await InitializeSessionAsync();
    }

    /// <summary>
    /// Emphasises the active destination by switching its icon to the filled
    /// variant; the pane's own selection indicator remains the primary affordance.
    /// </summary>
    private void OnNavigationSelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        foreach (var item in sender.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Icon is SymbolIcon symbolIcon)
            {
                symbolIcon.Filled = ReferenceEquals(item, sender.SelectedItem);
            }
        }
    }

    private void TryNavigateToLogin(ClientSession session)
    {
        session.Role = string.Empty;
        App.Services.GetRequiredService<Wpf.Ui.INavigationService>().Navigate(typeof(LoginPage));
    }

    private async Task InitializeSessionAsync()
    {
        var configError = App.Services.GetService<WpfConfigErrorState>();
        if (configError is not null)
        {
            ShowConfigError(configError.Message);
            return;
        }

        var tokenStore = App.Services.GetRequiredService<ISessionTokenStore>();
        var stored = await tokenStore.GetAsync();

        if (stored is not null)
        {
            // Refresh token and discover role.
            var auth = App.Services.GetRequiredService<AuthApiClient>();
            var meResult = await auth.GetAuthMeAsync();
            if (meResult.IsSuccess && meResult.Data is not null)
            {
                var session = App.Session;
                session.Role = meResult.Data.Role;
                session.IdentityType = meResult.Data.IdentityType;
                session.Username = meResult.Data.Username;
                session.DisplayName = meResult.Data.DisplayName;
                session.LastLoginAtUtc = DateTime.UtcNow;
                ViewModel.RefreshMenu();
                NavigateToDashboard();
                return;
            }

            await tokenStore.ClearAsync();
        }

        NavigateToLogin();
    }

    public void ShowConfigError(string message)
    {
        // Show error in a simple page or dialog; navigate to login for now.
        var state = App.Services.GetRequiredService<WpfConnectionState>();
        state.ErrorMessage = message;
        NavigateToLogin();
    }

    private void NavigateToDashboard() =>
        App
            .Services.GetRequiredService<Wpf.Ui.INavigationService>()
            .Navigate(typeof(DashboardPage));

    private void NavigateToLogin() =>
        App.Services.GetRequiredService<Wpf.Ui.INavigationService>().Navigate(typeof(LoginPage));
}
