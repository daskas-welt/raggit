using System;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T021: SessionTokenStore persists and restores tokens via ISecureStorage.
/// </summary>
public sealed class SessionTokenStoreTests
{
    [Fact]
    public async Task SaveAndGet_ReturnsTokenAndExpiry()
    {
        var store = new SessionTokenStore(new InMemorySecureStorage());
        var expires = DateTimeOffset.UtcNow.AddHours(8);

        await store.SaveAsync("abc123", expires);
        var session = await store.GetAsync();

        session.Should().NotBeNull();
        session!.Token.Should().Be("abc123");
        session.ExpiresAt.Should().BeCloseTo(expires, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Get_ExpiredToken_ReturnsNullAndClearsStore()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("expired", DateTimeOffset.UtcNow.AddMinutes(-1));

        var session = await store.GetAsync();

        session.Should().BeNull();
        (await storage.GetAsync("raggit.session.token")).Should().BeNull();
    }

    [Fact]
    public async Task Clear_RemovesTokenAndExpiry()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("to-clear", DateTimeOffset.UtcNow.AddHours(1));

        await store.ClearAsync();

        (await storage.GetAsync("raggit.session.token")).Should().BeNull();
        (await storage.GetAsync("raggit.session.expires_at")).Should().BeNull();
        (await store.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Save_NullOrEmptyToken_Throws()
    {
        var store = new SessionTokenStore(new InMemorySecureStorage());

        var act = () => store.SaveAsync(" ", DateTimeOffset.UtcNow.AddHours(1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// T032: a new process (new store instance over the same storage) restores the
    /// still-valid session without re-typing credentials.
    /// </summary>
    [Fact]
    public async Task SaveAndGet_NewStoreOverSameStorage_RestoresSession()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        await store.SaveAsync("process-kill-token", expires);

        // Simulate process restart: a brand-new store instance reads the same storage.
        var restartedStore = new SessionTokenStore(storage);
        var session = await restartedStore.GetAsync();

        session.Should().NotBeNull();
        session!.Token.Should().Be("process-kill-token");
        session.ExpiresAt.Should().BeCloseTo(expires, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// T032: expiry is honoured — a token past its expiry is treated as absent and
    /// removed from storage.
    /// </summary>
    [Fact]
    public async Task Get_TokenPastExpiry_ReturnsNullAndClearsStore()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("nearly-expired", DateTimeOffset.UtcNow.AddTicks(1));

        await Task.Delay(10);
        var session = await store.GetAsync();

        session.Should().BeNull();
        (await storage.GetAsync("raggit.session.token")).Should().BeNull();
    }

    /// <summary>
    /// T032: the session store never fabricates a session from an API key or any
    /// other fallback when the person token is missing.
    /// </summary>
    [Fact]
    public async Task Get_TokenMissingButOtherKeysPresent_ReturnsNull()
    {
        var storage = new InMemorySecureStorage();
        await storage.SetAsync(
            "raggit.session.expires_at",
            DateTimeOffset
                .UtcNow.AddHours(1)
                .ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        );
        await storage.SetAsync("some-api-key", "fallback-key");

        var store = new SessionTokenStore(storage);
        var session = await store.GetAsync();

        session.Should().BeNull();
    }
}
