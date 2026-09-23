using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.WinUI.Services;
using RAGGit.Client.WinUI.Views;
using Windows.Graphics;

namespace RAGGit.Client.WinUI;

public sealed partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private bool _synchronizingNavigation;

    public MainWindow()
    {
        InitializeComponent();
        App.MainWindow = this;

        // Capture HWND for FileOpenPicker interop.
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinUIFilePicker.OwnerHwnd = hwnd;

        // WinUI 3 has no SizeToContent: size explicitly (DIPs via rubric —
        // multi-pane nav + content shell ≈ 1200x800, rounded to 20).
        // AppWindow.Resize takes physical pixels, so scale by monitor DPI.
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1200 * scale), (int)(800 * scale)));

        // Native Windows 11 chrome: Mica backdrop (graceful solid fallback
        // on Windows 10) with content drawn into the title bar area.
        // Pages keep opaque backgrounds for now; they opt into layer fills
        // in their US2–US5 reskins so Mica shows through.
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
    }

    public void ShowConfigError(string message)
    {
        ConfigErrorText.Text = message;
        ConfigErrorOverlay.Visibility = Visibility.Visible;
    }

    public async Task InitializeSessionAsync()
    {
        var store = App.Services.GetRequiredService<ISessionTokenStore>();
        var cached = await store.GetAsync();

        if (cached is not null)
        {
            // Opportunistically refresh when expiring within 15 minutes.
            if (cached.ExpiresAt - DateTimeOffset.UtcNow < TimeSpan.FromMinutes(15))
            {
                var auth = App.Services.GetRequiredService<AuthApiClient>();
                var refresh = await auth.RefreshAsync();
                if (!refresh.IsSuccess)
                {
                    await store.ClearAsync();
                    NavigateToLogin();
                    return;
                }
            }

            await DiscoverRoleAsync();
            NavigateToDashboard();
            return;
        }

        NavigateToLogin();
    }

    public void NavigateToLogin() => ContentFrame.Navigate(typeof(LoginPage));

    public void NavigateToDashboard()
    {
        NavigateTo(typeof(DashboardPage), DashboardItem);
    }

    public void NavigateToLibrary()
    {
        NavigateTo(typeof(LibraryPage), LibraryItem);
    }

    public void NavigateToQuery()
    {
        NavigateTo(typeof(QueryPage), AskItem);
    }

    private void NavigateTo(Type pageType, NavigationViewItem navigationItem)
    {
        _synchronizingNavigation = true;
        try
        {
            Nav.SelectedItem = navigationItem;
            ContentFrame.Navigate(pageType);
            RefreshAdminVisibility();
        }
        finally
        {
            _synchronizingNavigation = false;
        }
    }

    public async Task DiscoverRoleAsync()
    {
        var session = App.Services.GetService<ClientSession>();
        var auth = App.Services.GetService<AuthApiClient>();
        var connection = App.Services.GetService<WinUIConnectionState>();
        if (session is null || auth is null)
        {
            return;
        }

        var result = await auth.GetAuthMeAsync();
        if (result.IsSuccess && result.Data is not null)
        {
            session.Role = result.Data.Role;
            session.IdentityType = result.Data.IdentityType;
            session.Username = result.Data.Username;
            session.DisplayName = result.Data.DisplayName;
            if (connection is not null)
            {
                connection.ErrorMessage = null;
                connection.RetryAction = null;
            }

            RefreshAdminVisibility();
        }
        else if (result.IsUnauthorized)
        {
            session.Role = string.Empty;
            session.Username = null;
            session.DisplayName = null;
            if (connection is not null)
            {
                connection.ErrorMessage = result.ErrorMessage ?? "unauthorized — sign in again.";
            }
        }
        else if (result.IsUnavailable)
        {
            session.Role = string.Empty;
            if (connection is not null)
            {
                connection.ErrorMessage = result.ErrorMessage ?? "AI workstation unavailable";
                connection.RetryAction = async () => await DiscoverRoleAsync();
            }
        }
        else if (connection is not null)
        {
            connection.ErrorMessage = result.ErrorMessage;
        }
    }

    private void RefreshAdminVisibility()
    {
        var session = App.Services.GetService<ClientSession>();
        var isAdmin = string.Equals(session?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        AdminItem.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Nav_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args
    )
    {
        if (_synchronizingNavigation)
        {
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var session = App.Services.GetService<ClientSession>();
        var isAdmin = string.Equals(session?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

        switch (tag)
        {
            case "dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                break;
            case "library":
                ContentFrame.Navigate(typeof(LibraryPage));
                break;
            case "query":
                ContentFrame.Navigate(typeof(QueryPage));
                break;
            case "history":
                ContentFrame.Navigate(typeof(HistoryPage));
                break;
            case "mine":
                ContentFrame.Navigate(typeof(DocumentsMinePage));
                break;
            case "admin" when isAdmin:
                ContentFrame.Navigate(typeof(AdminUsersPage));
                break;
        }
    }

    private void Nav_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Nav.PaneDisplayMode =
            e.NewSize.Width <= 720
                ? NavigationViewPaneDisplayMode.Top
                : NavigationViewPaneDisplayMode.Left;
    }
}
