using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_ComposesExistingCountsAndRecentItems()
    {
        var vm = CreateViewModel();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.TotalDocuments.Should().Be(2);
        vm.ReadyDocuments.Should().Be(1);
        vm.IndexingDocuments.Should().Be(1);
        vm.FailedDocuments.Should().Be(0);
        vm.TotalQueries.Should().Be(3);
        vm.MyDocuments.Should().Be(1);
        vm.RecentDocuments.Should().ContainSingle(d => d.Filename == "new.pdf");
        vm.RecentQueries.Should().ContainSingle(q => q.PromptPreview == "Where is the policy?");
        vm.HasLibraryData.Should().BeTrue();
        vm.HasQueryData.Should().BeTrue();
        vm.ErrorMessage.Should().BeNull();
        vm.StatusMessage.Should().Be("Dashboard refreshed");
    }

    [Fact]
    public async Task LoadAsync_EmptySourcesExposeIntentionalEmptyState()
    {
        var vm = CreateViewModel(empty: true);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.TotalDocuments.Should().Be(0);
        vm.TotalQueries.Should().Be(0);
        vm.MyDocuments.Should().Be(0);
        vm.RecentDocuments.Should().BeEmpty();
        vm.RecentQueries.Should().BeEmpty();
        vm.HasLibraryData.Should().BeFalse();
        vm.HasQueryData.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_FailureIsVisibleAndDoesNotFabricateAggregates()
    {
        var vm = CreateViewModel(fail: true);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        vm.HasStatus.Should().BeTrue();
        vm.TotalDocuments.Should().Be(0);
        vm.TotalQueries.Should().Be(0);
        vm.MyDocuments.Should().Be(0);
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_DuplicateExecutionIsGated()
    {
        var handler = new BlockingHandler();
        var vm = CreateViewModel(handler);

        var first = vm.LoadCommand.ExecuteAsync(null);
        await handler.Started.Task;
        var second = vm.LoadCommand.ExecuteAsync(null);

        second.IsCompleted.Should().BeTrue();
        handler.Release();
        await first;
    }

    private static DashboardViewModel CreateViewModel(bool empty = false, bool fail = false) =>
        CreateViewModel(new DashboardHandler(empty, fail));

    private static DashboardViewModel CreateViewModel(HttpMessageHandler handler)
    {
        var documents = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation/") }
        );
        var history = new QueryHistoryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation/") }
        );
        return new DashboardViewModel(
            documents,
            history,
            new ClientSession { WorkstationUrl = "https://workstation", ApiKey = "key" }
        );
    }

    private sealed class DashboardHandler : HttpMessageHandler
    {
        private readonly bool _empty;
        private readonly bool _fail;

        public DashboardHandler(bool empty, bool fail)
        {
            _empty = empty;
            _fail = fail;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (_fail)
            {
                throw new HttpRequestException("cannot reach AI workstation: test failure");
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body =
                path.EndsWith("/documents", StringComparison.Ordinal)
                    ? (_empty ? "[]" : DocumentsJson)
                : path.EndsWith("/history", StringComparison.Ordinal)
                    ? (_empty ? HistoryEmptyJson : HistoryJson)
                : (_empty ? MineEmptyJson : MineJson);

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Started.TrySetResult(true);
            await _release.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            };
        }

        public void Release() => _release.TrySetResult(true);
    }

    private const string DocumentsJson = """
        [
          {"id":"00000000-0000-0000-0000-000000000001","filename":"old.txt","mime":"text/plain","size":12,"hash":"a","status":"Ready","createdBy":"u","createdAt":"2026-09-20T00:00:00Z"},
          {"id":"00000000-0000-0000-0000-000000000002","filename":"new.pdf","mime":"application/pdf","size":24,"hash":"b","status":"Indexing","createdBy":"u","createdAt":"2026-09-21T00:00:00Z"}
        ]
        """;

    private const string HistoryJson = """
        {"items":[{"id":"00000000-0000-0000-0000-000000000003","promptPreview":"Where is the policy?","answerPreview":"In the library","citationCount":1,"latencyMs":20,"createdAt":"2026-09-21T00:00:00Z"}],"total":3,"limit":5,"offset":0}
        """;

    private const string HistoryEmptyJson = """
        {"items":[],"total":0,"limit":5,"offset":0}
        """;

    private const string MineJson = """
        {"items":[],"total":1,"limit":1,"offset":0}
        """;

    private const string MineEmptyJson = """
        {"items":[],"total":0,"limit":1,"offset":0}
        """;
}
