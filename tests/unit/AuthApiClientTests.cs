using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T023: AuthApiClient login/refresh against /api/auth endpoints.
/// </summary>
public sealed class AuthApiClientTests
{
    [Fact]
    public async Task LoginAsync_Success_SavesTokenAndReturnsExpiry()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/auth/login");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            access_token = "token-a",
                            token_type = "Bearer",
                            expires_in = 28800,
                        }
                    )
                ),
            };
        });
        var client = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") },
            store
        );

        var result = await client.LoginAsync(
            new LoginRequest { Username = "bob", Password = "pass" }
        );

        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("token-a");
        result.ExpiresAt.Should().NotBeNull();

        var session = await store.GetAsync();
        session.Should().NotBeNull();
        session!.Token.Should().Be("token-a");
    }

    [Fact]
    public async Task LoginAsync_Unauthorized_ReturnsUnauthorizedWithoutSaving()
    {
        var store = new SessionTokenStore(new InMemorySecureStorage());
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") },
            store
        );

        var result = await client.LoginAsync(
            new LoginRequest { Username = "bob", Password = "wrong" }
        );

        result.IsSuccess.Should().BeFalse();
        result.IsUnauthorized.Should().BeTrue();
        (await store.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_NetworkError_ReturnsUnavailable()
    {
        var handler = new TestMessageHandler(_ => throw new HttpRequestException("No connection"));
        var client = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );

        var result = await client.LoginAsync(
            new LoginRequest { Username = "bob", Password = "pass" }
        );

        result.IsSuccess.Should().BeFalse();
        result.IsUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_Success_UpdatesStoredToken()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("old-token", DateTimeOffset.UtcNow.AddHours(1));

        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/auth/refresh");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            access_token = "new-token",
                            token_type = "Bearer",
                            expires_in = 28800,
                        }
                    )
                ),
            };
        });
        var client = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") },
            store
        );

        var result = await client.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        result.Token.Should().Be("new-token");
        (await store.GetAsync())!.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task RefreshAsync_Unauthorized_ClearsStore()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("expired-token", DateTimeOffset.UtcNow.AddHours(1));

        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = new AuthApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") },
            store
        );

        var result = await client.RefreshAsync();

        result.IsUnauthorized.Should().BeTrue();
        (await store.GetAsync()).Should().BeNull();
    }

    private sealed class TestMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_handler(request));
    }
}
