using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Stores the workstation-signed bearer token and its expiry in platform secure storage.
/// </summary>
public interface ISessionTokenStore
{
    Task SaveAsync(
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    );

    Task<StoredSession?> GetAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A recovered session credential.
/// </summary>
public sealed record StoredSession(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Minimal secure-storage abstraction so the token store can be unit-tested without
/// platform secure storage.
/// </summary>
public interface ISecureStorage
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);

    void RemoveAll();
}

/// <summary>
/// SecureStorage-backed session token cache. Keys: raggit.session.token, raggit.session.expires_at.
/// </summary>
public sealed class SessionTokenStore : ISessionTokenStore
{
    private const string TokenKey = "raggit.session.token";
    private const string ExpiresKey = "raggit.session.expires_at";

    private readonly ISecureStorage _secureStorage;

    public SessionTokenStore(ISecureStorage secureStorage)
    {
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
    }

    public async Task SaveAsync(
        string token,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await _secureStorage.SetAsync(TokenKey, token);
        await _secureStorage.SetAsync(
            ExpiresKey,
            expiresAt.ToString("O", CultureInfo.InvariantCulture)
        );
    }

    public async Task<StoredSession?> GetAsync(CancellationToken cancellationToken = default)
    {
        var token = await _secureStorage.GetAsync(TokenKey);
        var expires = await _secureStorage.GetAsync(ExpiresKey);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expires))
        {
            return null;
        }

        if (
            !DateTimeOffset.TryParseExact(
                expires,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expiresAt
            )
        )
        {
            await ClearAsync(cancellationToken);
            return null;
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            await ClearAsync(cancellationToken);
            return null;
        }

        return new StoredSession(token, expiresAt);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _secureStorage.Remove(TokenKey);
        _secureStorage.Remove(ExpiresKey);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory secure storage for the headless build used by unit tests.
/// </summary>
public sealed class InMemorySecureStorage : ISecureStorage
{
    private readonly System.Collections.Generic.Dictionary<string, string> _store = new();

    public Task<string?> GetAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => _store.Remove(key);

    public void RemoveAll() => _store.Clear();
}
