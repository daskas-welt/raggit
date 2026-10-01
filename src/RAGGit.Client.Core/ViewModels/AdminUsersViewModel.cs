using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.ViewModels;

/// <summary>
/// ViewModel for the Admin-only people management screen.
/// </summary>
public sealed partial class AdminUsersViewModel : ObservableObject
{
    private readonly UsersApiClient _usersApiClient;

    [ObservableProperty]
    private ObservableCollection<UserAccountDto> _users = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Dedicated status surface for the Admin page (016-admin-redesign):
    /// every outcome — success, failure with reason and next step — is
    /// mirrored here. Severity is a UI-framework-free string
    /// (Informational, Success, Warning, Error); the WinUI layer maps it
    /// to InfoBarSeverity, same pattern as UploadViewModel. ErrorMessage
    /// is kept as the compatibility surface and always matches Status
    /// whenever Severity is Error or Warning.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _statusSeverity = "Informational";

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    /// <summary>
    /// Empty-state visibility: shown only when idle and the list is empty,
    /// so "No users found" never competes with the loading spinner during
    /// refresh.
    /// </summary>
    public bool ShowEmptyUsers => !IsBusy && Users.Count == 0;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyUsers));

    partial void OnUsersChanged(ObservableCollection<UserAccountDto> value)
    {
        if (_subscribedUsers is not null)
        {
            _subscribedUsers.CollectionChanged -= OnUsersCollectionChanged;
        }
        _subscribedUsers = value;
        if (value is not null)
        {
            value.CollectionChanged += OnUsersCollectionChanged;
        }
        OnPropertyChanged(nameof(ShowEmptyUsers));
    }

    private ObservableCollection<UserAccountDto>? _subscribedUsers;

    private void OnUsersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(ShowEmptyUsers));

    [ObservableProperty]
    private UserAccountDto? _selectedUser;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newDisplayName = string.Empty;

    [ObservableProperty]
    private UserRole _newRole = UserRole.Employee;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _resetPassword = string.Empty;

    [ObservableProperty]
    private bool _resetMustChangePassword;

    [ObservableProperty]
    private bool _passwordResetCompleted;

    public IReadOnlyList<UserRole> Roles { get; } = new[] { UserRole.Admin, UserRole.Employee };

    public AdminUsersViewModel(UsersApiClient usersApiClient)
    {
        _usersApiClient = usersApiClient ?? throw new ArgumentNullException(nameof(usersApiClient));
        // The field initializer does not run the generated OnUsersChanged
        // hook, so subscribe to the initial collection here.
        _subscribedUsers = Users;
        Users.CollectionChanged += OnUsersCollectionChanged;
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        IsBusy = true;
        ClearStatus();

        try
        {
            var users = await _usersApiClient.GetUsersAsync();
            Users = new ObservableCollection<UserAccountDto>(users);
            SetSuccess(Users.Count == 1 ? "Loaded 1 user." : $"Loaded {Users.Count} users.");
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to load users: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            SetWarning("Username is required. Enter a unique login name.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewDisplayName))
        {
            SetWarning("Display name is required. Enter the name shown in the users list.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            SetWarning("Initial password is required. Enter a password for the new account.");
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var created = await _usersApiClient.CreateUserAsync(
                new CreateUserRequest
                {
                    Username = NewUsername.Trim(),
                    DisplayName = NewDisplayName.Trim(),
                    Role = NewRole,
                    Password = NewPassword,
                }
            );

            Users.Add(created);
            NewUsername = string.Empty;
            NewDisplayName = string.Empty;
            NewPassword = string.Empty;
            NewRole = UserRole.Employee;
            SetSuccess($"Created user '{created.Username}'.");
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to create user: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChangeRoleAsync(UserAccountDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var newRole = user.Role == UserRole.Admin ? UserRole.Employee : UserRole.Admin;
            var updated = await _usersApiClient.UpdateUserAsync(
                user.Id,
                new UpdateUserRequest { Role = newRole }
            );
            UpdateUserInCollection(updated);
            SetSuccess($"Changed '{updated.Username}' from {user.Role} to {newRole}.");
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to change role: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChangeRoleToAsync(UserRoleChangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.User);

        if (IsBusy)
        {
            return;
        }

        if (request.User.Role == request.Role)
        {
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var updated = await _usersApiClient.UpdateUserAsync(
                request.User.Id,
                new UpdateUserRequest { Role = request.Role }
            );
            UpdateUserInCollection(updated);
            SetSuccess($"Changed '{updated.Username}' from {request.User.Role} to {request.Role}.");
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to change role: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(UserAccountDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            var updated = await _usersApiClient.UpdateUserAsync(
                user.Id,
                new UpdateUserRequest { IsActive = !user.IsActive }
            );
            UpdateUserInCollection(updated);
            SetSuccess(
                updated.IsActive
                    ? $"Activated '{updated.Username}'."
                    : $"Deactivated '{updated.Username}'."
            );
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to update active status: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserAccountDto? user)
    {
        PasswordResetCompleted = false;
        if (IsBusy)
        {
            return;
        }

        if (user is null)
        {
            SetWarning("Select a user first. Choose a user in the list, then reset.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ResetPassword))
        {
            SetWarning("Enter a new password to reset.");
            return;
        }

        IsBusy = true;
        ClearStatus();

        try
        {
            await _usersApiClient.ResetPasswordAsync(
                user.Id,
                new ResetUserPasswordRequest
                {
                    Password = ResetPassword,
                    MustChangePassword = ResetMustChangePassword,
                }
            );

            PasswordResetCompleted = true;
            ResetPassword = string.Empty;
            ResetMustChangePassword = false;
            await LoadUsersAsync();

            if (ErrorMessage is null)
            {
                SetSuccess($"Password reset for '{user.Username}'.");
            }
            else
            {
                var refreshError = ErrorMessage;
                SetWarning(
                    $"Password reset for '{user.Username}', but the people list could not refresh: {refreshError}"
                );
            }
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            SetError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            SetError(ClientErrorText.CannotReach(ex.Message));
        }
        catch (TaskCanceledException ex)
        {
            SetError(ClientErrorText.Unavailable(ex.Message));
        }
        catch (Exception ex)
        {
            SetError($"Failed to reset password: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateUserInCollection(UserAccountDto updated)
    {
        var existing = Users.FirstOrDefault(u => u.Id == updated.Id);
        if (existing is not null)
        {
            var index = Users.IndexOf(existing);
            Users[index] = updated;
            if (SelectedUser?.Id == updated.Id)
            {
                SelectedUser = updated;
            }
        }
    }

    public void ClearStatus()
    {
        ErrorMessage = null;
        StatusMessage = null;
        StatusSeverity = "Informational";
    }

    private void SetSuccess(string message)
    {
        ErrorMessage = null;
        StatusMessage = message;
        StatusSeverity = "Success";
    }

    private void SetWarning(string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        StatusSeverity = "Warning";
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        StatusSeverity = "Error";
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.StatusCode == System.Net.HttpStatusCode.Conflict
        || ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase);
}

public sealed record UserRoleChangeRequest(UserAccountDto User, UserRole Role);
