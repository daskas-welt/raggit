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
}
