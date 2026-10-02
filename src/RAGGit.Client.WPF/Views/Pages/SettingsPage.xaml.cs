using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var session = App.Services.GetRequiredService<ClientSession>();
        WorkstationUrlText.Text = string.IsNullOrWhiteSpace(session.WorkstationUrl)
            ? "(not configured)"
            : session.WorkstationUrl;
        SignedInAsText.Text = string.IsNullOrWhiteSpace(session.Username)
            ? session.IdentityType
            : session.Username;

        var connection = App.Services.GetRequiredService<WpfConnectionState>();
        ConnectionStatusText.Text =
            connection.IsUnavailable ? $"Unavailable: {connection.ErrorMessage}"
            : string.IsNullOrWhiteSpace(connection.ErrorMessage) ? "Connected"
            : connection.ErrorMessage;
        ConnectionStatusIcon.Symbol = connection.IsUnavailable
            ? SymbolRegular.PlugDisconnected24
            : SymbolRegular.PlugConnected24;
        ConnectionStatusIcon.SetResourceReference(
            System.Windows.Controls.Control.ForegroundProperty,
            connection.IsUnavailable
                ? "SystemFillColorCriticalBrush"
                : "SystemFillColorSuccessBrush"
        );

        var theme = ApplicationThemeManager.GetAppTheme();
        LightThemeRadio.IsChecked = theme == ApplicationTheme.Light;
        DarkThemeRadio.IsChecked = theme != ApplicationTheme.Light;
    }

    private void OnLightThemeChecked(object sender, RoutedEventArgs e) =>
        ApplicationThemeManager.Apply(ApplicationTheme.Light);

    private void OnDarkThemeChecked(object sender, RoutedEventArgs e) =>
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

    private async void OnSignOutClicked(object sender, RoutedEventArgs e)
    {
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (!await dialogs.ConfirmAsync("Sign out", "Sign out of this session?"))
        {
            return;
        }

        var store = App.Services.GetRequiredService<ISessionTokenStore>();
        await store.ClearAsync();
        App.Services.GetRequiredService<INavigationService>().NavigateToLogin();
    }
}
