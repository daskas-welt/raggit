using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 034 US1: the typed creation client posts the description and format and
/// returns the binary result without touching the 033 answer-export client.
/// </summary>
public sealed class DocumentCreationApiClientTests
{
    [Fact]
    public async Task CreateAsync_PostsDescriptionAndFormat_ReturnsFile()
    {
        string? requestBody = null;
        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/documents/generate");
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello")),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                "attachment"
            )
            {
                FileNameStar = "welcome.txt",
            };
            return response;
        });

        var client = new DocumentCreationApiClient(NewHttpClient(handler));

        var file = await client.CreateAsync("Draft a welcome note.", DocumentOutputFormat.Txt);

        using var json = JsonDocument.Parse(requestBody!);
        json.RootElement.GetProperty("description")
            .GetString()
            .Should()
            .Be("Draft a welcome note.");
        json.RootElement.GetProperty("format").GetString().Should().Be("txt");
        file.FileName.Should().Be("welcome.txt");
        file.ContentType.Should().Be("text/plain");
        Encoding.UTF8.GetString(file.Bytes).Should().Be("hello");
    }

    [Fact]
    public async Task CreateAsync_ServiceUnavailable_MapsToTheExistingVocabulary()
    {
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable
        )
        {
            Content = JsonContent("AI workstation unavailable"),
        });

        var client = new DocumentCreationApiClient(NewHttpClient(handler));

        var act = () => client.CreateAsync("Draft a note.", DocumentOutputFormat.Txt);

        await act.Should()
            .ThrowAsync<HttpRequestException>()
            .WithMessage("*AI workstation unavailable*");
    }

    [Fact]
    public async Task CreateAsync_BadRequest_SurfacesTheServerMessage()
    {
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent("description is required"),
        });

        var client = new DocumentCreationApiClient(NewHttpClient(handler));

        var act = () => client.CreateAsync("Draft a note.", DocumentOutputFormat.Txt);

        await act.Should()
            .ThrowAsync<HttpRequestException>()
            .WithMessage("*description is required*");
    }

    [Fact]
    public async Task CreateAsync_CallerCancellation_Propagates()
    {
        var handler = new TestMessageHandler(_ => throw new OperationCanceledException());
        var client = new DocumentCreationApiClient(NewHttpClient(handler));

        var act = () =>
            client.CreateAsync("Draft a note.", DocumentOutputFormat.Txt, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://workstation.local/") };

    private static StringContent JsonContent(string error) =>
        new($"{{\"error\":\"{error}\"}}", Encoding.UTF8, "application/json");

    private sealed class TestMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder(request));
    }
}
