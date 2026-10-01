using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Thin client for the Admin-only /api/users people-management endpoints.
/// </summary>
public sealed class UsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public UsersApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// GET /api/users
    /// </summary>
    public async Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetAsync("api/users", cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var users = await response.Content.ReadFromJsonAsync<List<UserAccountDto>>(
                _jsonOptions,
                cancellationToken
            );
            return users ?? new List<UserAccountDto>();
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    /// <summary>
    /// POST /api/users
    /// </summary>
    public async Task<UserAccountDto> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/users",
                request,
                _jsonOptions,
                cancellationToken
            );
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var user =
                await response.Content.ReadFromJsonAsync<UserAccountDto>(
                    _jsonOptions,
                    cancellationToken
                ) ?? throw new InvalidOperationException("Failed to deserialize user response.");
            return user;
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    /// <summary>
    /// PATCH /api/users/{id}
    /// </summary>
    public async Task<UserAccountDto> UpdateUserAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await _httpClient.PatchAsJsonAsync(
                $"api/users/{id}",
                request,
                _jsonOptions,
                cancellationToken
            );
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var user =
                await response.Content.ReadFromJsonAsync<UserAccountDto>(
                    _jsonOptions,
                    cancellationToken
                ) ?? throw new InvalidOperationException("Failed to deserialize user response.");
            return user;
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    /// <summary>
    /// POST /api/users/{id}/reset-password
    /// </summary>
    public async Task ResetPasswordAsync(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/users/{id}/reset-password",
                request,
                _jsonOptions,
                cancellationToken
            );
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (
            response.StatusCode == HttpStatusCode.ServiceUnavailable
            && body.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new HttpRequestException("model unavailable offline");
        }
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException($"AI workstation unavailable: {body}");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new HttpRequestException($"unauthorized: {body}");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException($"forbidden: {body}");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new HttpRequestException(
                GetConflictMessage(body),
                inner: null,
                statusCode: HttpStatusCode.Conflict
            );
        response.EnsureSuccessStatusCode();
    }

    private static string GetConflictMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (
                document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(error.GetString())
            )
            {
                return error.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall back to the response body when it is not the standard Error shape.
        }

        return $"conflict: {body}";
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.StatusCode == HttpStatusCode.Conflict
        || ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Person account returned by /api/users (PasswordHash is never serialized).
/// </summary>
public sealed class UserAccountDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public bool LockedOut { get; set; }
    public DateTime? LastSignInAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request body for POST /api/users.
/// </summary>
public sealed class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Request body for PATCH /api/users/{id}.
/// </summary>
public sealed class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request body for POST /api/users/{id}/reset-password.
/// </summary>
public sealed class ResetUserPasswordRequest
{
    public string Password { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
