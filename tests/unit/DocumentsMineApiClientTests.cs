using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T027: DocumentsApiClient reads GET /api/documents/mine pages (005-per-person-history US3).
/// </summary>
public sealed class DocumentsMineApiClientTests
{
    [Fact]
    public async Task GetMineAsync_Success_ReturnsPage()
    {
        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/documents/mine?limit=20&offset=0");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            items = new[]
                            {
                                new
                                {
                                    id = Guid.NewGuid(),
                                    filename = "own-doc.txt",
                                    size = 128,
                                    status = "Ready",
                                    createdAt = DateTime.UtcNow,
                                },
                            },
                            total = 1,
                            limit = 20,
                            offset = 0,
                        }
                    )
                ),
            };
        });
        var client = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );

        var page = await client.GetMineAsync(20, 0);

        page.Total.Should().Be(1);
        page.Limit.Should().Be(20);
        page.Offset.Should().Be(0);
        page.Items.Should().ContainSingle().Which.Filename.Should().Be("own-doc.txt");
    }

    [Fact]
    public async Task GetMineAsync_Unauthorized_Throws()
    {
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );

        Func<Task> act = () => client.GetMineAsync(20, 0);

        (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage("*unauthorized*");
    }

    private sealed class TestMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder(request));
    }
}
