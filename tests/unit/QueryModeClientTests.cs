using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T026: Client mode plumbing — <see cref="QueryViewModel.QueryMode"/>
/// defaults to <see cref="QueryMode.Auto"/> and
/// <see cref="QueryApiClient.QueryAsync"/> serializes the selected mode
/// while surfacing the echoed effective intent.
/// </summary>
public sealed class QueryModeClientTests
{
    [Fact]
    public void QueryMode_DefaultsToAuto()
    {
        var viewModel = CreateViewModel(_ => Task.FromResult(OkAnswer("broad")));

        viewModel.QueryMode.Should().Be(QueryMode.Auto);
    }

    [Fact]
    public async Task QueryAsync_SendsSelectedMode()
    {
        string? capturedBody = null;
        var handler = new CapturingHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return OkAnswer("broad");
        });
        var apiClient = new QueryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );

        await apiClient.QueryAsync("renewal term", mode: QueryMode.Broad);

        capturedBody.Should().NotBeNull();
        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("mode").GetString().Should().Be("broad");
    }

    [Fact]
    public async Task QueryAsync_SurfacesEchoedEffectiveIntent()
    {
        var apiClient = new QueryApiClient(
            new HttpClient(new CapturingHandler(_ => Task.FromResult(OkAnswer("broad"))))
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );

        var response = await apiClient.QueryAsync("renewal term", mode: QueryMode.Broad);

        response.Mode.Should().Be(QueryIntent.Broad);
    }

    [Fact]
    public async Task Ask_SendsSelectedViewModelMode()
    {
        string? capturedBody = null;
        var viewModel = CreateViewModel(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return OkAnswer("granular");
        });
        viewModel.QueryMode = QueryMode.Specific;
        viewModel.QueryText = "Summarize the refund policy";

        await viewModel.AskCommand.ExecuteAsync(null);

        capturedBody.Should().NotBeNull();
        using var json = JsonDocument.Parse(capturedBody!);
        json.RootElement.GetProperty("mode").GetString().Should().Be("specific");
    }

    private static QueryViewModel CreateViewModel(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond
    )
    {
        var apiClient = new QueryApiClient(
            new HttpClient(new CapturingHandler(respond))
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var historyHandler = new CapturingHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            })
        );
        return new QueryViewModel(
            apiClient,
            new QueryHistoryApiClient(
                new HttpClient(historyHandler) { BaseAddress = new Uri("https://w.local/") }
            ),
            new ClientSession(),
            new ConversationStore()
        );
    }

    private static HttpResponseMessage OkAnswer(string mode) =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        answer = "an answer",
                        citations = Array.Empty<object>(),
                        retrievedChunkIds = Array.Empty<Guid>(),
                        latencyMs = 7,
                        suggestedPersons = Array.Empty<object>(),
                        mode,
                    }
                )
            ),
        };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;

        public CapturingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) =>
            _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => _respond(request);
    }
}
