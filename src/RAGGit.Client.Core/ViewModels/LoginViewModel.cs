using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RAGGit.Client.Maui.Services;

namespace RAGGit.Client.Maui.ViewModels;

/// <summary>
/// ViewModel for the per-person HTTPS sign-in screen. Enforces HTTPS-only login,
/// surfaces actionable certificate-trust errors, and never provides an insecure
/// certificate-validation bypass.
/// </summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly AuthApiClient _authApiClient;
    private readonly ClientSession _session;
    private readonly Func<Task>? _onLoginSuccess;
    private readonly INavigationService? _navigationService;

    [ObservableProperty]
    private string _workstationUrl = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    partial void OnErrorMessageChanged(string? value) =>
        HasError = !string.IsNullOrWhiteSpace(value);

    public LoginViewModel(
        AuthApiClient authApiClient,
        ClientSession session,
        Func<Task>? onLoginSuccess = null,
        INavigationService? navigationService = null
    )
    {
        _authApiClient = authApiClient ?? throw new ArgumentNullException(nameof(authApiClient));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _onLoginSuccess = onLoginSuccess;
        _navigationService = navigationService;
        WorkstationUrl = session.WorkstationUrl;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(WorkstationUrl))
        {
            ErrorMessage = "Workstation URL is required.";
            return;
        }

        if (!Uri.TryCreate(WorkstationUrl, UriKind.Absolute, out var uri))
        {
            ErrorMessage = "Workstation URL is not valid.";
            return;
        }

        // HTTPS-only for per-person credentials/tokens (FR-008). localhost is allowed
        // only for single-machine development, never for production LAN traffic.
        if (
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        )
        {
            ErrorMessage =
                "HTTPS is required for sign-in. Configure Workstation:Url to an https:// address and trust the workstation certificate.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return;
        }

        IsBusy = true;

        try
        {
            var loginResult = await _authApiClient.LoginAsync(
                new LoginRequest { Username = Username.Trim(), Password = Password }
            );

            if (loginResult.IsSuccess)
            {
                var meResult = await _authApiClient.GetAuthMeAsync();
                if (meResult.IsSuccess && meResult.Data is not null)
                {
                    _session.Role = meResult.Data.Role;
                    _session.IdentityType = meResult.Data.IdentityType;
                    _session.Username = meResult.Data.Username;
                    _session.DisplayName = meResult.Data.DisplayName;
                    _session.LastLoginAtUtc = DateTime.UtcNow;
                    ErrorMessage = null;

                    if (_navigationService is not null)
                    {
                        _navigationService.NavigateToDashboard();
                    }
                    else if (_onLoginSuccess is not null)
                    {
                        await _onLoginSuccess();
                    }

                    return;
                }

                ErrorMessage =
                    meResult.ErrorMessage ?? "Signed in, but identity discovery failed. Try again.";
                return;
            }

            if (loginResult.IsUnauthorized)
            {
                ErrorMessage = "Sign-in failed. Check your username and password.";
                return;
            }

            if (IsCertificateErrorMessage(loginResult.ErrorMessage))
            {
                ErrorMessage =
                    "Could not establish a secure connection. Ask the operator to install and trust the workstation certificate. Plain HTTP and certificate bypass are not allowed.";
                return;
            }

            ErrorMessage = loginResult.ErrorMessage ?? "Sign-in failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsCertificateErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
            || message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
            || message.Contains("trust", StringComparison.OrdinalIgnoreCase)
            || message.Contains("remote certificate", StringComparison.OrdinalIgnoreCase);
    }
}
