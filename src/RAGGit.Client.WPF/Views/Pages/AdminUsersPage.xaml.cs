using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Client.WPF.Views.Dialogs;
using RAGGit.Core.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace RAGGit.Client.WPF.Views.Pages;

public partial class AdminUsersPage : Page
{
    private ICollectionView? _usersView;
    private string _sortProperty = nameof(UserAccountDto.DisplayName);
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private bool? _compactLayout;
    private bool _restoringRoleSelection;
    private readonly SearchSessionState _searchSession;
    private bool _searchReady;

    public AdminUsersViewModel ViewModel { get; }

    public AdminUsersPage()
    {
        ViewModel = App.Services.GetRequiredService<AdminUsersViewModel>();
        _searchSession = App.Services.GetRequiredService<SearchSessionState>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BindUsersView();
        var savedSearch = _searchSession.Get(SearchSurface.People);
        if (!string.IsNullOrEmpty(savedSearch))
        {
            UserSearchBox.Text = savedSearch;
        }

        // Combo selection during InitializeComponent also raises OnFiltersChanged.
        // Ignore those writes so they cannot blank a saved search before it is restored.
        _searchReady = true;
        UpdateSortIndicator();
        ApplyResponsiveLayout(AdminLayout.ActualWidth);
        if (ViewModel.Users.Count == 0 && !ViewModel.IsBusy)
        {
            _ = ViewModel.LoadUsersCommand.ExecuteAsync(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminUsersViewModel.Users))
        {
            BindUsersView();
        }
        else if (
            e.PropertyName
            is nameof(AdminUsersViewModel.IsBusy)
                or nameof(AdminUsersViewModel.ShowEmptyUsers)
        )
        {
            UpdateDirectorySummary();
        }
    }

    private void BindUsersView()
    {
        var selectedId = ViewModel.SelectedUser?.Id;
        _usersView = CollectionViewSource.GetDefaultView(ViewModel.Users);
        _usersView.Filter = MatchesCurrentFilters;
        _usersView.SortDescriptions.Clear();
        _usersView.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
        UsersList.ItemsSource = _usersView;
        ViewModel.SelectedUser = selectedId is Guid id
            ? ViewModel.Users.FirstOrDefault(user => user.Id == id)
            : null;
        RefreshUsersView();
    }

    private bool MatchesCurrentFilters(object item)
    {
        if (item is not UserAccountDto user)
        {
            return false;
        }

        var search = UserSearchBox.Text?.Trim();
        if (
            !string.IsNullOrEmpty(search)
            && !user.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
            && !user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var role = SelectedTag(RoleFilterCombo);
        if (role != "All" && !string.Equals(user.Role.ToString(), role, StringComparison.Ordinal))
        {
            return false;
        }

        return SelectedTag(StatusFilterCombo) switch
        {
            "Active" => user.IsActive,
            "Disabled" => !user.IsActive,
            _ => true,
        };
    }

    private static string SelectedTag(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";

    private void OnFiltersChanged(object sender, RoutedEventArgs e)
    {
        if (_searchReady)
        {
            _searchSession.Set(SearchSurface.People, UserSearchBox.Text);
        }

        RefreshUsersView();
    }

    private void RefreshUsersView()
    {
        if (_usersView is null)
        {
            return;
        }

        _usersView.Refresh();
        var matches = _usersView.Cast<UserAccountDto>().ToList();
        if (string.IsNullOrWhiteSpace(UserSearchBox.Text))
        {
            // No search: a person the role/status filters removed must clear the
            // details panel, as before 029.
            if (ViewModel.SelectedUser is { } selected && !matches.Contains(selected))
            {
                ViewModel.SelectedUser = null;
            }
        }
        else
        {
            // While a search is active, highlight the first match so the details
            // panel offers that person's actions without a row click (029, FR-011).
            // A person the user already highlighted stays highlighted while they
            // remain visible.
            ViewModel.SelectedUser = PeopleSearchHighlight.Resolve(
                UserSearchBox.Text,
                ViewModel.SelectedUser,
                matches
            );
        }

        UpdateDirectorySummary();
    }

    private void OnClearFiltersClicked(object sender, RoutedEventArgs e)
    {
        UserSearchBox.Clear();
        RoleFilterCombo.SelectedIndex = 0;
        StatusFilterCombo.SelectedIndex = 0;
        RefreshUsersView();
    }

    private void OnUserColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (
            e.OriginalSource is not GridViewColumnHeader header
            || header.Tag is not string property
        )
        {
            return;
        }

        if (property == _sortProperty)
        {
            _sortDirection =
                _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
        }
        else
        {
            _sortProperty = property;
            _sortDirection = ListSortDirection.Ascending;
        }

        if (_usersView is not null)
        {
            _usersView.SortDescriptions.Clear();
            _usersView.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            _usersView.Refresh();
        }

        var direction = _sortDirection == ListSortDirection.Ascending ? "ascending" : "descending";
        SortStatusText.Text = $"Sorted by {ColumnLabel(property)}, {direction}";
        UpdateSortIndicator();
    }

    /// <summary>
    /// The pinned header shows the active sort from first load, not only after a click (FR-008).
    /// </summary>
    private void UpdateSortIndicator()
    {
        var arrow = _sortDirection == ListSortDirection.Ascending ? "\u2191" : "\u2193";
        foreach (var column in PeopleHeader.Children.OfType<GridViewColumnHeader>())
        {
            var label = ColumnLabel(column.Tag as string);
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            column.Content = column.Tag as string == _sortProperty ? $"{label} {arrow}" : label;
        }
    }

    private static string ColumnLabel(string? tag) =>
        tag switch
        {
            "DisplayName" => "Person",
            "Role" => "Role",
            "IsActive" => "Account status",
            _ => string.Empty,
        };

    private void UpdateDirectorySummary()
    {
        if (_usersView is null)
        {
            return;
        }

        var visibleCount = _usersView.Cast<object>().Count();
        VisibleUsersText.Text = visibleCount == 1 ? "1 person" : $"{visibleCount} people";
        FilteredEmptyState.Visibility =
            ViewModel.Users.Count > 0 && visibleCount == 0 && !ViewModel.IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ApplyPeopleColumnWidths(bool compact)
    {
        if (
            UsersList.View is not System.Windows.Controls.GridView gridView
            || gridView.Columns.Count < 4
        )
        {
            return;
        }

        gridView.Columns[1].Width = compact ? 0 : 135;
        gridView.Columns[2].Width = compact ? 0 : 190;
        gridView.Columns[3].Width = compact ? 0 : 132;
        PeopleHeader.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 135);
        PeopleHeader.ColumnDefinitions[2].Width = new GridLength(compact ? 0 : 190);
        PeopleHeader.ColumnDefinitions[3].Width = new GridLength(compact ? 0 : 132);
    }

    private void OnAdminLayoutSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width <= 720;
        // Null until the first call, so the initial layout is always applied
        // (the XAML carries the wide default, but the first measurement wins).
        if (compact == _compactLayout)
        {
            return;
        }

        _compactLayout = compact;
        // Role, status, and the row action collapse at narrow widths; the Person
        // column stays, and the details panel keeps the collapsed information
        // reachable (029, FR-009, FR-010).
        ApplyPeopleColumnWidths(compact);
        if (compact)
        {
            PeopleLayout.ColumnDefinitions[1].Width = new GridLength(0);
            PeopleLayout.RowDefinitions[0].Height = new GridLength(2, GridUnitType.Star);
            PeopleLayout.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(DirectoryPanel, 0);
            Grid.SetColumnSpan(DirectoryPanel, 2);
            Grid.SetRow(DetailPanel, 1);
            Grid.SetColumn(DetailPanel, 0);
            Grid.SetColumnSpan(DetailPanel, 2);
            DirectoryPanel.Margin = new Thickness(0, 0, 0, 8);
            DetailPanel.Margin = new Thickness(0);
        }
        else
        {
            PeopleLayout.ColumnDefinitions[1].Width = new GridLength(320);
            PeopleLayout.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            PeopleLayout.RowDefinitions[1].Height = new GridLength(0);
            Grid.SetColumn(DirectoryPanel, 0);
            Grid.SetColumnSpan(DirectoryPanel, 1);
            Grid.SetRow(DetailPanel, 0);
            Grid.SetColumn(DetailPanel, 1);
            Grid.SetColumnSpan(DetailPanel, 1);
            DirectoryPanel.Margin = new Thickness(0, 0, 8, 0);
            DetailPanel.Margin = new Thickness(8, 0, 0, 0);
        }
    }

    private async void OnRoleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (
            _restoringRoleSelection
            || sender is not ComboBox combo
            || combo.DataContext is not UserAccountDto user
            || e.AddedItems.Count == 0
            || e.AddedItems[0] is not UserRole requestedRole
            || requestedRole == user.Role
        )
        {
            return;
        }

        _restoringRoleSelection = true;
        try
        {
            combo.SelectedItem = user.Role;
        }
        finally
        {
            _restoringRoleSelection = false;
        }

        if (ViewModel.IsBusy)
        {
            return;
        }

        var isSelf = string.Equals(
            App.Session.Username,
            user.Username,
            StringComparison.OrdinalIgnoreCase
        );
        var consequence = isSelf
            ? " This is your own account; changing your role may end your current access."
            : string.Empty;
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            !await dialogs.ConfirmAsync(
                "Change role",
                $"Change '{user.Username}' from {user.Role} to {requestedRole}?{consequence}"
            )
        )
        {
            return;
        }

        await ViewModel.ChangeRoleToCommand.ExecuteAsync(
            new UserRoleChangeRequest(user, requestedRole)
        );
        RefreshUsersView();
    }

    private async void OnToggleActiveClicked(object sender, RoutedEventArgs e)
    {
        if (
            ViewModel.IsBusy || (sender as FrameworkElement)?.DataContext is not UserAccountDto user
        )
        {
            return;
        }

        var verb = user.IsActive ? "Deactivate" : "Activate";
        var isSelf =
            user.IsActive
            && string.Equals(
                App.Session.Username,
                user.Username,
                StringComparison.OrdinalIgnoreCase
            );
        var consequence = isSelf
            ? " This is your own account; you may be signed out after the change."
            : string.Empty;
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            await dialogs.ConfirmAsync(
                $"{verb} user",
                $"Are you sure you want to {verb.ToLowerInvariant()} '{user.Username}'?{consequence}"
            )
        )
        {
            await ViewModel.ToggleActiveCommand.ExecuteAsync(user);
            RefreshUsersView();
        }
    }

    private async void OnAddPersonClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy)
        {
            return;
        }

        var contentDialogs = App.Services.GetRequiredService<IContentDialogService>();
        var form = new CreatePersonDialog(ViewModel);
        var dialog = new ContentDialog { Title = "Add person", Content = form };
        form.RequestClose += (_, _) => dialog.Hide();
        await contentDialogs.ShowAsync(dialog, CancellationToken.None);
        RefreshUsersView();
        AddPersonButton.Focus();
    }

    private async void OnResetPasswordClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsBusy || ViewModel.SelectedUser is not UserAccountDto user)
        {
            return;
        }

        // Confirm before showing the form so the in-window confirm never nests
        // inside the form dialog.
        var dialogs = App.Services.GetRequiredService<IDialogService>();
        if (
            !await dialogs.ConfirmAsync(
                "Reset password",
                $"Reset the password for '{user.Username}'?"
            )
        )
        {
            return;
        }

        var contentDialogs = App.Services.GetRequiredService<IContentDialogService>();
        var form = new ResetPasswordDialog(ViewModel, user);
        var dialog = new ContentDialog { Title = "Reset password", Content = form };
        form.RequestClose += (_, _) => dialog.Hide();
        await contentDialogs.ShowAsync(dialog, CancellationToken.None);
        RefreshUsersView();
    }
}
