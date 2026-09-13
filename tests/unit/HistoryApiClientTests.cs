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
/// T010: QueryHistoryApiClient reads GET /api/queries/history pages.
/// </summary>
public sealed class HistoryApiClientTests
{
    [Fact]
    public async Task GetHistoryAsync_Success_ReturnsPage()
    {
        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().Be("/api/queries/history?limit=20&offset=0");
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
                                    promptPreview = "own prompt",
                                    answerPreview = "own answer",
                                    citationCount = 2,
                                    latencyMs = 9,
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
        var client = new QueryHistoryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );

        var page = await client.GetHistoryAsync(20, 0);

        page.Total.Should().Be(1);
        page.Limit.Should().Be(20);
        page.Offset.Should().Be(0);
        page.Items.Should().ContainSingle().Which.PromptPreview.Should().Be("own prompt");
    }

    [Fact]
    public async Task GetHistoryAsync_Unauthorized_Throws()
    {
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = new QueryHistoryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );

        Func<Task> act = () => client.GetHistoryAsync(20, 0);

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
