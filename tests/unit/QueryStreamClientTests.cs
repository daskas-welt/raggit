using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 032 T014: the NDJSON reader behind <see cref="QueryApiClient.StreamAsync"/> —
/// it yields events in order, tolerates junk lines, keeps the classic
/// pre-stream error mapping, and refuses to invent an answer for a truncated
/// stream.
/// </summary>
public sealed class QueryStreamClientTests
{
    [Fact]
    public async Task StreamAsync_YieldsEventsInArrivalOrder()
    {
        var client = ClientFor(
            Body(
                "{\"type\":\"progress\",\"stage\":\"understanding\",\"text\":\"Reading your question…\"}",
                "{\"type\":\"progress\",\"stage\":\"searching\",\"text\":\"Searching the library…\"}",
                "{\"type\":\"progress\",\"stage\":\"sources\",\"text\":\"Found 1 relevant source: a.pdf\",\"sourceCount\":1,\"documentNames\":[\"a.pdf\"]}",
                "{\"type\":\"progress\",\"stage\":\"composing\",\"text\":\"Writing your answer…\"}",
                "{\"type\":\"answer\",\"answer\":\"30 days\",\"citations\":[],\"retrievedChunkIds\":[],\"latencyMs\":3}"
            )
        );

        var events = await CollectAsync(client.StreamAsync("What is the renewal term?"));

        events.Should().HaveCount(5);
        events
            .Select(e => e.Stage)
            .Should()
            .Equal(
                QueryStage.Understanding,
                QueryStage.Searching,
                QueryStage.Sources,
                QueryStage.Composing,
                null
            );
        events[2].SourceCount.Should().Be(1);
        events[2].DocumentNames.Should().Equal("a.pdf");
        events[4].IsAnswer.Should().BeTrue();
        events[4].Answer.Should().Be("30 days");
        events[4].ToResponse().LatencyMs.Should().Be(3);
    }

    [Fact]
    public async Task StreamAsync_SkipsBlankMalformedAndUnknownLines_WithoutAborting()
    {
        var client = ClientFor(
            Body(
                "",
                "{\"type\":\"progress\",\"stage\":\"searching\",\"text\":\"Searching the library…\"}",
                "{not json at all",
                "{\"type\":\"telemetry\",\"stage\":\"unknown-to-this-client\"}",
                "{\"type\":\"answer\",\"answer\":\"done\",\"citations\":[],\"retrievedChunkIds\":[],\"latencyMs\":1}"
            )
        );

        var events = await CollectAsync(client.StreamAsync("q"));

        events.Should().HaveCount(2);
        events[0].Stage.Should().Be(QueryStage.Searching);
        events[1].Answer.Should().Be("done");
    }

    [Fact]
    public async Task StreamAsync_ParsesAFinalLineWithoutATrailingNewline()
    {
        var client = ClientFor(
            "{\"type\":\"progress\",\"stage\":\"composing\",\"text\":\"Writing your answer…\"}\n"
                + "{\"type\":\"answer\",\"answer\":\"done\",\"citations\":[],\"retrievedChunkIds\":[],\"latencyMs\":1}"
        );

        var events = await CollectAsync(client.StreamAsync("q"));

        events.Should().HaveCount(2);
        events[1].IsAnswer.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_WithoutATerminalEvent_ThrowsInsteadOfReturningAnEmptyAnswer()
    {
        var client = ClientFor(
            Body("{\"type\":\"progress\",\"stage\":\"searching\",\"text\":\"Searching…\"}")
        );

        Func<Task> act = () => CollectAsync(client.StreamAsync("q"));

        await act.Should()
            .ThrowAsync<HttpRequestException>()
            .WithMessage("*ended before the answer was received*");
    }

    [Fact]
    public async Task StreamAsync_MapsAPreStream503_ToTheClassicMessage()
    {
        var client = ClientFor(
            "{\"error\":\"model unavailable offline\"}",
            HttpStatusCode.ServiceUnavailable
        );

        Func<Task> act = () => CollectAsync(client.StreamAsync("q"));

        await act.Should()
            .ThrowAsync<HttpRequestException>()
            .WithMessage("model unavailable offline");
    }

    [Fact]
    public async Task StreamAsync_MapsAPreStream503_ToWorkstationUnavailable()
    {
        var client = ClientFor(
            "{\"error\":\"AI workstation unavailable\"}",
            HttpStatusCode.ServiceUnavailable
        );

        Func<Task> act = () => CollectAsync(client.StreamAsync("q"));

        await act.Should()
            .ThrowAsync<HttpRequestException>()
            .WithMessage("AI workstation unavailable*");
    }

    [Fact]
    public async Task StreamAsync_YieldsATerminalErrorEvent()
    {
        var client = ClientFor(
            Body(
                "{\"type\":\"progress\",\"stage\":\"searching\",\"text\":\"Searching…\"}",
                "{\"type\":\"error\",\"error\":\"model unavailable offline\",\"status\":503}"
            )
        );

        var events = await CollectAsync(client.StreamAsync("q"));

        events.Should().HaveCount(2);
        events[1].IsError.Should().BeTrue();
        events[1].Error.Should().Be("model unavailable offline");
        events[1].Status.Should().Be(503);
    }

    [Fact]
    public async Task StreamAsync_PostsToTheStreamEndpoint()
    {
        string? body = null;
        Uri? uri = null;
        var handler = new StubHttpHandler(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            uri = request.RequestUri;
            return Ok(
                Body(
                    "{\"type\":\"answer\",\"answer\":\"done\",\"citations\":[],\"retrievedChunkIds\":[],\"latencyMs\":1}"
                )
            );
        });
        var client = new QueryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );

        await CollectAsync(client.StreamAsync("renewal term"));

        uri!.AbsolutePath.Should().Be("/api/queries/stream");
        using var json = System.Text.Json.JsonDocument.Parse(body!);
        json.RootElement.TryGetProperty("mode", out _).Should().BeFalse();
        json.RootElement.GetProperty("query").GetString().Should().Be("renewal term");
    }

    [Fact]
    public void ClientApi_NoLongerAcceptsOrExposesAMode()
    {
        typeof(QueryApiClient)
            .GetMethod(nameof(QueryApiClient.StreamAsync))!
            .GetParameters()
            .Select(parameter => parameter.Name)
            .Should()
            .NotContain("mode");
        typeof(QueryApiClient)
            .GetMethod(nameof(QueryApiClient.QueryAsync))!
            .GetParameters()
            .Select(parameter => parameter.Name)
            .Should()
            .NotContain("mode");
        typeof(QueryViewModel).GetProperty("QueryMode").Should().BeNull();
    }

    private static QueryApiClient ClientFor(
        string body,
        HttpStatusCode status = HttpStatusCode.OK
    ) =>
        new(
            new HttpClient(new StubHttpHandler(_ => Ok(body, status)))
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );

    private static string Body(params string[] lines) => string.Join("\n", lines) + "\n";

    private static HttpResponseMessage Ok(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson") };

    private static async Task<List<QueryStreamEvent>> CollectAsync(
        IAsyncEnumerable<QueryStreamEvent> events
    )
    {
        var collected = new List<QueryStreamEvent>();
        await foreach (var streamEvent in events)
        {
            collected.Add(streamEvent);
        }

        return collected;
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_respond(request));
    }
}
