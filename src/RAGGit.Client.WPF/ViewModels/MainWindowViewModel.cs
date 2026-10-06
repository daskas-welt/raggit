using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RAGGit.Client.Core;
using RAGGit.Client.WPF.Views.Pages;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.ViewModels;

/// <summary>
/// ViewModel for the WPF main window shell. Owns the navigation menu items and
/// reacts to session role changes.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ClientSession _session;

    [ObservableProperty]
    private ObservableCollection<object> _menuItems = new();

    [ObservableProperty]
    private ObservableCollection<object> _footerMenuItems = new();

    [ObservableProperty]
    private string _windowTitle = "RAGGit";

    [ObservableProperty]
    private bool _isAdmin;

    public MainWindowViewModel(ClientSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClientSession.Role))
            {
                RefreshMenu();
            }
        };
        RefreshMenu();
    }

    public void RefreshMenu()
    {
        IsAdmin = _session.IsAdmin;

        var items = new List<object>
        {
            CreateItem("Dashboard", SymbolRegular.Home24, typeof(DashboardPage), "NavDashboard"),
            CreateItem("Document Library", SymbolRegular.Library24, typeof(LibraryPage), "NavLibrary"),
            CreateItem("Ask", SymbolRegular.Chat24, typeof(QueryPage), "NavAsk"),
            CreateItem("History", SymbolRegular.History24, typeof(HistoryPage), "NavHistory"),
            CreateItem("My Documents", SymbolRegular.Document24, typeof(DocumentsMinePage), "NavMyDocs"),
        };

        if (IsAdmin)
        {
            items.Add(
                CreateItem("Admin", SymbolRegular.People24, typeof(AdminUsersPage), "NavAdmin")
            );
        }

        MenuItems = new ObservableCollection<object>(items);

        FooterMenuItems = new ObservableCollection<object>
        {
            CreateItem("Settings", SymbolRegular.Settings24, typeof(SettingsPage), "NavSettings"),
        };
    }

    /// <summary>
    /// Builds a navigation item with an explicit <see cref="SymbolIcon"/> so the
    /// shell can switch it to the filled variant while the destination is active.
    /// </summary>
    private static NavigationViewItem CreateItem(
        string content,
        SymbolRegular icon,
        Type pageType,
        string automationId
    )
    {
        var item = new NavigationViewItem(content, pageType)
        {
            Icon = new SymbolIcon { Symbol = icon },
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(item, automationId);
        return item;
    }
}
