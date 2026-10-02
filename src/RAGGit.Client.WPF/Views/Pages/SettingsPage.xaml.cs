using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.WPF.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class SettingsPage : Page
{
    private bool _isRechecking;

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

        RenderConnectionStatus();

        var theme = ApplicationThemeManager.GetAppTheme();
        LightThemeRadio.IsChecked = theme == ApplicationTheme.Light;
        DarkThemeRadio.IsChecked = theme != ApplicationTheme.Light;
    }

    /// <summary>
    /// Shared connection-status presentation: the same text/icon/brush mapping
    /// on load and after every re-check, plus the last-checked moment.
    /// </summary>
    private void RenderConnectionStatus()
    {
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
        ConnectionLastCheckedText.Text = connection.LastCheckedAtUtc is { } checkedAt
            ? $"Last checked {checkedAt.ToLocalTime():g}"
            : string.Empty;
    }

    private async void OnRecheckConnectionClicked(object sender, RoutedEventArgs e)
    {
        if (_isRechecking)
        {
            return;
        }

        _isRechecking = true;
        SetRecheckBusy(true);
        try
        {
            // The existing GET /api/auth/me probe (also used at startup);
            // no new API surface.
            var auth = App.Services.GetRequiredService<AuthApiClient>();
            var result = await auth.GetAuthMeAsync();
            var connection = App.Services.GetRequiredService<WpfConnectionState>();
            connection.ErrorMessage = result.IsSuccess
                ? null
                : result.ErrorMessage ?? "Connection check failed.";
            connection.LastCheckedAtUtc = DateTimeOffset.UtcNow;
            RenderConnectionStatus();
        }
        finally
        {
            SetRecheckBusy(false);
            _isRechecking = false;
        }
    }

    private void SetRecheckBusy(bool busy)
    {
        SettingsRecheckConnectionButton.IsEnabled = !busy;
        RecheckStatusIcon.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        RecheckStatusRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCopyWorkstationUrlClicked(object sender, RoutedEventArgs e)
    {
        // The "(not configured)" placeholder is not a value worth copying.
        if (WorkstationUrlText.Text == "(not configured)")
        {
            return;
        }

        CopyValue(WorkstationUrlText.Text, "Workstation URL");
    }

    private void OnCopySignedInAsClicked(object sender, RoutedEventArgs e) =>
        CopyValue(SignedInAsText.Text, "Signed-in account");

    private static void CopyValue(string? text, string what)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            App.Services.GetRequiredService<INotificationService>()
                .Show("Copied", $"{what} copied to clipboard.", NotificationKind.Success);
        }
        catch
        {
            // Clipboard may be locked by another process; a failed copy
            // stays silent rather than confirming what did not happen.
        }
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
