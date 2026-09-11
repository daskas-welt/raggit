using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// T020: Thin client for GET /api/auth/me (1.1.0). Deserializes AuthMe envelope
/// with unknown fields ignored (Q2 forward-compat). Maps 401 → config-error signal,
/// HttpRequestException/TaskCanceledException → unavailable signal.
/// </summary>
public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        // System.Text.Json ignores unknown fields by default; explicit for clarity
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    public AuthApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

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
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable && body.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase))
                    return AuthMeResult.Unavailable("model unavailable offline");
                return AuthMeResult.Failure($"auth/me failed: {(int)response.StatusCode} {body}");
            }

            var authMe = await response.Content.ReadFromJsonAsync<AuthMe>(_jsonOptions, cancellationToken);
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

public sealed class AuthMe
{
    public string IdentityType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

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
    public static AuthMeResult Unauthorized(string? msg = null) => new(false, null, AuthMeErrorKind.Unauthorized, msg ?? "unauthorized");
    public static AuthMeResult Unavailable(string? msg = null) => new(false, null, AuthMeErrorKind.Unavailable, msg ?? "cannot reach AI workstation");
    public static AuthMeResult Failure(string msg) => new(false, null, AuthMeErrorKind.Unknown, msg);

    public bool IsUnauthorized => ErrorKind == AuthMeErrorKind.Unauthorized;
    public bool IsUnavailable => ErrorKind == AuthMeErrorKind.Unavailable;
}

public enum AuthMeErrorKind
{
    None,
    Unauthorized,
    Unavailable,
    Unknown
}
