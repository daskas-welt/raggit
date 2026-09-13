using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T022: BearerDelegatingHandler attaches cached tokens and clears on 401.
/// </summary>
public sealed class BearerDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_WithCachedToken_AttachesBearerHeader()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("abc-token", DateTimeOffset.UtcNow.AddHours(8));

        var inner = new TestMessageHandler(request =>
        {
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Scheme.Should().Be("Bearer");
            request.Headers.Authorization.Parameter.Should().Be("abc-token");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new BearerDelegatingHandler(store) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://workstation.local/api/documents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendAsync_NoCachedToken_DoesNotAddAuthorizationHeader()
    {
        var store = new SessionTokenStore(new InMemorySecureStorage());
        AuthorizationHeaderValue? captured = null;
        var inner = new TestMessageHandler(request =>
        {
            captured = request.Headers.Authorization is null
                ? null
                : new AuthorizationHeaderValue(
                    request.Headers.Authorization.Scheme,
                    request.Headers.Authorization.Parameter
                );
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new BearerDelegatingHandler(store) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        await client.GetAsync("https://workstation.local/api/documents");

        captured.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_ServerReturns401_ClearsCachedToken()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("expired-token", DateTimeOffset.UtcNow.AddHours(8));

        var inner = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var handler = new BearerDelegatingHandler(store) { InnerHandler = inner };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://workstation.local/api/documents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await store.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_AlreadyHasAuthorizationHeader_DoesNotReplace()
    {
        var storage = new InMemorySecureStorage();
        var store = new SessionTokenStore(storage);
        await store.SaveAsync("cached-token", DateTimeOffset.UtcNow.AddHours(8));

        var inner = new TestMessageHandler(request =>
        {
            request.Headers.Authorization!.Scheme.Should().Be("Basic");
            request.Headers.Authorization.Parameter.Should().Be("preset");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var handler = new BearerDelegatingHandler(store) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", "preset");
        await client.GetAsync("https://workstation.local/api/documents");
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

    private sealed record AuthorizationHeaderValue(string Scheme, string? Parameter);
}
