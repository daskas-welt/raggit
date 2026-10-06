using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 029-client-ux-optimization T006: History search filters the loaded items
/// only, re-applies after LoadMore, and counts loaded matches rather than the
/// server total (FR-001, FR-004, contracts U1.4).
/// </summary>
public sealed class HistorySearchTests
{
    [Fact]
    public async Task Search_MatchesPromptAndAnswer_CaseInsensitively()
    {
        var vm = await LoadedAsync(total: 3, pageSize: 3);

        vm.SearchText = "ANSWER 1";

        vm.FilteredItems.Should().ContainSingle().Which.AnswerPreview.Should().Be("answer 1");
        vm.MatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_CountsLoadedMatches_NotTheServerTotal()
    {
        // Three records exist on the server; only the first page of two is loaded.
        var vm = await LoadedAsync(total: 3, pageSize: 2);

        vm.SearchText = "prompt";

        vm.MatchCount.Should().Be(2);
        vm.Total.Should().Be(3);
    }

    [Fact]
    public async Task LoadMore_ReappliesTheFilter()
    {
        var vm = await LoadedAsync(total: 3, pageSize: 2);
        vm.SearchText = "prompt 2";
        vm.FilteredItems.Should().BeEmpty();

        await vm.LoadMoreCommand.ExecuteAsync(null);

        vm.FilteredItems.Should().ContainSingle().Which.PromptPreview.Should().Be("prompt 2");
        vm.MatchCount.Should().Be(1);
    }

    [Fact]
    public async Task Refresh_ReappliesTheCurrentSearch()
    {
        var vm = await LoadedAsync(total: 3, pageSize: 3);
        vm.SearchText = "prompt 0";

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.SearchText.Should().Be("prompt 0");
        vm.FilteredItems.Should().ContainSingle();
    }

    [Fact]
    public void Search_SeedsFromSessionState_AndWritesBack()
    {
        var session = new SearchSessionState();
        session.Set(SearchSurface.History, "persisted");
        var vm = CreateViewModel(total: 0, pageSize: 2, session);

        vm.SearchText.Should().Be("persisted");

        vm.SearchText = "changed";

        session.Get(SearchSurface.History).Should().Be("changed");
    }

    private static async Task<HistoryViewModel> LoadedAsync(int total, int pageSize)
    {
        var vm = CreateViewModel(total, pageSize, new SearchSessionState());
        await vm.LoadCommand.ExecuteAsync(null);
        return vm;
    }

    private static HistoryViewModel CreateViewModel(
        int total,
        int pageSize,
        SearchSessionState session
    )
    {
        var handler = new TestMessageHandler(request =>
        {
            var (limit, offset) = ParsePaging(request.RequestUri!.Query, pageSize);
            var items = Enumerable
                .Range(offset, Math.Max(0, Math.Min(limit, total - offset)))
                .Select(i => new
                {
                    id = Guid.NewGuid(),
                    promptPreview = $"prompt {i}",
                    answerPreview = $"answer {i}",
                    citationCount = i,
                    latencyMs = 5,
                    createdAt = DateTime.UtcNow,
                })
                .ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            items,
                            total,
                            limit,
                            offset,
                        }
                    )
                ),
            };
        });
        var apiClient = new QueryHistoryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );
        var viewModel = new HistoryViewModel(apiClient, session) { Limit = pageSize };
        return viewModel;
    }

    private static (int Limit, int Offset) ParsePaging(string query, int defaultLimit)
    {
        var limit = defaultLimit;
        var offset = 0;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "limit" && int.TryParse(parts[1], out var l))
            {
                limit = l;
            }
            else if (parts.Length == 2 && parts[0] == "offset" && int.TryParse(parts[1], out var o))
            {
                offset = o;
            }
        }

        return (limit, offset);
    }

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
