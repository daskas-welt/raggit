using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.ViewModels;

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

    public IReadOnlyList<UserRole> Roles { get; } = new[] { UserRole.Admin, UserRole.Employee };

    public AdminUsersViewModel(UsersApiClient usersApiClient)
    {
        _usersApiClient = usersApiClient ?? throw new ArgumentNullException(nameof(usersApiClient));
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var users = await _usersApiClient.GetUsersAsync();
            Users = new ObservableCollection<UserAccountDto>(users);
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load users: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        if (
            string.IsNullOrWhiteSpace(NewUsername)
            || string.IsNullOrWhiteSpace(NewDisplayName)
            || string.IsNullOrWhiteSpace(NewPassword)
        )
        {
            ErrorMessage = "Username, display name, and password are required.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

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
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create user: {ex.Message}";
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

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var newRole = user.Role == UserRole.Admin ? UserRole.Employee : UserRole.Admin;
            var updated = await _usersApiClient.UpdateUserAsync(
                user.Id,
                new UpdateUserRequest { Role = newRole }
            );
            UpdateUserInCollection(updated);
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to change role: {ex.Message}";
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

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var updated = await _usersApiClient.UpdateUserAsync(
                user.Id,
                new UpdateUserRequest { IsActive = !user.IsActive }
            );
            UpdateUserInCollection(updated);
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update active status: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync(UserAccountDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(ResetPassword))
        {
            ErrorMessage = "Enter a new password to reset.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

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

            ResetPassword = string.Empty;
            ResetMustChangePassword = false;
            await LoadUsersAsync();
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"cannot reach AI workstation: {ex.Message}";
        }
        catch (TaskCanceledException ex)
        {
            ErrorMessage = $"AI workstation unavailable: {ex.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to reset password: {ex.Message}";
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
        }
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase);
}
