using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Thin client for /api/auth endpoints. Supports anonymous login, token refresh,
/// and additive GET /api/auth/me. Maps 401 → config-error signal and transport
/// failures → unavailable signal.
/// </summary>
public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ISessionTokenStore? _store;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        // System.Text.Json ignores unknown fields by default; explicit for clarity
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public AuthApiClient(HttpClient httpClient, ISessionTokenStore? store = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _store = store;
    }

    /// <summary>
    /// POST /api/auth/login — stores the returned token when successful.
    /// </summary>
    public async Task<AuthLoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/auth/login",
                request,
                _jsonOptions,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return AuthLoginResult.Unauthorized();
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthLoginResult.Failure($"account locked: {body}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthLoginResult.Failure($"login failed: {(int)response.StatusCode} {body}");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                _jsonOptions,
                cancellationToken
            );
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return AuthLoginResult.Failure("invalid token response");
            }

            var expiresAt =
                token.ExpiresIn > 0
                    ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
                    : DateTimeOffset.UtcNow.AddHours(8);

            if (_store is not null)
            {
                await _store.SaveAsync(token.AccessToken, expiresAt, cancellationToken);
            }

            return AuthLoginResult.Success(token.AccessToken, expiresAt);
        }
        catch (HttpRequestException ex)
        {
            return AuthLoginResult.Unavailable($"cannot reach AI workstation: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return AuthLoginResult.Unavailable($"AI workstation unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// POST /api/auth/refresh — re-issues an 8h token from a still-valid token.
    /// Clears the cache on 401.
    /// </summary>
    public async Task<AuthLoginResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync("api/auth/refresh", null, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (_store is not null)
                {
                    await _store.ClearAsync(cancellationToken);
                }

                return AuthLoginResult.Unauthorized();
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return AuthLoginResult.Failure(
                    $"refresh failed: {(int)response.StatusCode} {body}"
                );
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                _jsonOptions,
                cancellationToken
            );
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                return AuthLoginResult.Failure("invalid token response");
            }

            var expiresAt =
                token.ExpiresIn > 0
                    ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
                    : DateTimeOffset.UtcNow.AddHours(8);

            if (_store is not null)
            {
                await _store.SaveAsync(token.AccessToken, expiresAt, cancellationToken);
            }

            return AuthLoginResult.Success(token.AccessToken, expiresAt);
        }
        catch (HttpRequestException ex)
        {
            return AuthLoginResult.Unavailable($"cannot reach AI workstation: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return AuthLoginResult.Unavailable($"AI workstation unavailable: {ex.Message}");
        }
    }

    /// <summary>
    /// GET /api/auth/me
    /// </summary>
    public async Task<AuthMeResult> GetAuthMeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/auth/me", cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return AuthMeResult.Unauthorized();
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Treat 403 as unauthorized for role discovery (should not happen on auth/me)
                return AuthMeResult.Unauthorized();
            }
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // Map 503 model unavailable offline verbatim if present
                if (
                    response.StatusCode == HttpStatusCode.ServiceUnavailable
                    && body.Contains(
                        "model unavailable offline",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return AuthMeResult.Unavailable("model unavailable offline");
                return AuthMeResult.Failure($"auth/me failed: {(int)response.StatusCode} {body}");
            }

            var authMe = await response.Content.ReadFromJsonAsync<AuthMe>(
                _jsonOptions,
                cancellationToken
            );
            if (authMe is null || string.IsNullOrWhiteSpace(authMe.Role))
                return AuthMeResult.Failure("Invalid auth/me response");

            return AuthMeResult.Success(authMe);
        }
        catch (HttpRequestException ex)
        {
            return AuthMeResult.Unavailable($"cannot reach AI workstation: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            // HttpClient timeout maps to TaskCanceledException
            return AuthMeResult.Unavailable($"AI workstation unavailable: {ex.Message}");
        }
    }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public sealed class AuthMe
{
    public string IdentityType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Sub { get; set; }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public sealed class AuthMeResult
{
    public bool IsSuccess { get; }
    public AuthMe? Data { get; }
    public AuthMeErrorKind ErrorKind { get; }
    public string? ErrorMessage { get; }

    private AuthMeResult(bool isSuccess, AuthMe? data, AuthMeErrorKind kind, string? message)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorKind = kind;
        ErrorMessage = message;
    }

    public static AuthMeResult Success(AuthMe data) => new(true, data, AuthMeErrorKind.None, null);

    public static AuthMeResult Unauthorized(string? msg = null) =>
        new(false, null, AuthMeErrorKind.Unauthorized, msg ?? "unauthorized");

    public static AuthMeResult Unavailable(string? msg = null) =>
        new(false, null, AuthMeErrorKind.Unavailable, msg ?? "cannot reach AI workstation");

    public static AuthMeResult Failure(string msg) =>
        new(false, null, AuthMeErrorKind.Unknown, msg);

    public bool IsUnauthorized => ErrorKind == AuthMeErrorKind.Unauthorized;
    public bool IsUnavailable => ErrorKind == AuthMeErrorKind.Unavailable;
}

public enum AuthMeErrorKind
{
    None,
    Unauthorized,
    Unavailable,
    Unknown,
}

public sealed class AuthLoginResult
{
    public bool IsSuccess { get; }
    public string? Token { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public AuthLoginErrorKind ErrorKind { get; }
    public string? ErrorMessage { get; }

    private AuthLoginResult(
        bool isSuccess,
        string? token,
        DateTimeOffset? expiresAt,
        AuthLoginErrorKind kind,
        string? message
    )
    {
        IsSuccess = isSuccess;
        Token = token;
        ExpiresAt = expiresAt;
        ErrorKind = kind;
        ErrorMessage = message;
    }

    public static AuthLoginResult Success(string token, DateTimeOffset expiresAt) =>
        new(true, token, expiresAt, AuthLoginErrorKind.None, null);

    public static AuthLoginResult Unauthorized(string? msg = null) =>
        new(false, null, null, AuthLoginErrorKind.Unauthorized, msg ?? "unauthorized");

    public static AuthLoginResult Unavailable(string? msg = null) =>
        new(
            false,
            null,
            null,
            AuthLoginErrorKind.Unavailable,
            msg ?? "cannot reach AI workstation"
        );

    public static AuthLoginResult Failure(string msg) =>
        new(false, null, null, AuthLoginErrorKind.Unknown, msg);

    public bool IsUnauthorized => ErrorKind == AuthLoginErrorKind.Unauthorized;
    public bool IsUnavailable => ErrorKind == AuthLoginErrorKind.Unavailable;
}

public enum AuthLoginErrorKind
{
    None,
    Unauthorized,
    Unavailable,
    Unknown,
}
