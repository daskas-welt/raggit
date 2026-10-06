using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
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

        // A successful load of an empty library still confirms the zeros as real
        // data — the metric cards may show them.
        vm.HasLoaded.Should().BeTrue();
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
    public async Task LoadAsync_FirstLoadFailureLeavesDashboardUnloaded()
    {
        // specs/027 data-model: on a first-load failure no numbers are presented
        // as loaded data. HasLoaded stays false so the view can hide the metric
        // cards and the library summary instead of rendering the initial zeros.
        var vm = CreateViewModel(fail: true);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.HasLoaded.Should().BeFalse();
        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        vm.TotalDocuments.Should().Be(0);
        vm.TotalQueries.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_FailedRefreshAfterSuccessKeepsLoadedFlagAndLastKnownCounts()
    {
        // Last-known-good: after a successful load, a failed refresh keeps the
        // loaded flag and the counts, so the cards keep showing real numbers
        // rather than reverting to unloaded zeros.
        var handler = new FailAfterFirstLoadHandler();
        var vm = CreateViewModel(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.HasLoaded.Should().BeTrue();
        vm.TotalDocuments.Should().Be(2);
        vm.TotalQueries.Should().Be(3);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.HasLoaded.Should().BeTrue();
        vm.TotalDocuments.Should().Be(2);
        vm.TotalQueries.Should().Be(3);
        vm.StatusSeverity.Should().Be("Error");
        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
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

    [Fact]
    public async Task LoadAsync_RefreshOfEmptySourcesDoesNotResetSeverity()
    {
        // The whole-dashboard empty state is gated on StatusSeverity == "Success"
        // (DashboardPage.xaml). ClearStatus() runs at the start of every load, so if it
        // also reset the severity, the empty state would flicker off for the duration of
        // every refresh of an empty library. The last completed outcome must survive
        // ClearStatus and only change when a new outcome lands.
        var handler = new ToggleHandler();
        var vm = CreateViewModel(handler);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.HasLibraryData.Should().BeFalse();
        vm.HasQueryData.Should().BeFalse();
        vm.StatusSeverity.Should().Be("Success");

        var refresh = vm.LoadCommand.ExecuteAsync(null);
        await handler.Gated.Task;

        vm.IsBusy.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Success");

        handler.Release();
        await refresh;

        vm.StatusSeverity.Should().Be("Success");
    }

    [Fact]
    public async Task Refresh_Failure_HidesAttentionEvenWhenEarlierLoadHadFailures()
    {
        var vm = CreateViewModel(new FailOnRefreshHandler());

        await vm.LoadCommand.ExecuteAsync(null);
        vm.HasFailedDocuments.Should().BeTrue();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.StatusSeverity.Should().Be("Error");
        vm.HasFailedDocuments.Should().BeFalse();
        vm.FailedDocuments.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_CapsRecentItemsAtTen()
    {
        var vm = CreateViewModel(new ManyItemsHandler());

        await vm.LoadCommand.ExecuteAsync(null);

        vm.TotalDocuments.Should().Be(12);
        vm.TotalQueries.Should().Be(12);
        vm.RecentDocuments.Should().HaveCount(10);
        vm.RecentQueries.Should().HaveCount(10);
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

    /// <summary>First load returns one failed document; the refresh cannot reach the workstation.</summary>
    private sealed class FailOnRefreshHandler : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/documents", StringComparison.Ordinal) && ++_calls > 1)
            {
                throw new HttpRequestException("cannot reach AI workstation: refresh failed");
            }

            var body =
                path.EndsWith("/documents", StringComparison.Ordinal)
                    ? """
                        [{"id":"00000000-0000-0000-0000-000000000009","filename":"bad.pdf","mime":"application/pdf","size":1,"hash":"c","status":"Failed","createdBy":"u","createdAt":"2026-09-22T00:00:00Z"}]
                        """
                : path.EndsWith("/history", StringComparison.Ordinal) ? HistoryEmptyJson
                : MineEmptyJson;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                }
            );
        }
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

    /// <summary>
    /// Returns empty-but-successful responses; lets the first load (requests 1-3) through
    /// untouched and gates the second load (requests 4-6) so the test can inspect the
    /// in-flight state before the next outcome lands.
    /// </summary>
    private sealed class ToggleHandler : HttpMessageHandler
    {
        private int _calls;

        public TaskCompletionSource<bool> Gated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var call = Interlocked.Increment(ref _calls);
            if (call > 3)
            {
                Gated.TrySetResult(true);
                await _release.Task;
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body =
                path.EndsWith("/documents", StringComparison.Ordinal) ? "[]"
                : path.EndsWith("/history", StringComparison.Ordinal) ? HistoryEmptyJson
                : MineEmptyJson;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }

        public void Release() => _release.TrySetResult(true);
    }

    /// <summary>
    /// Serves the populated responses for the first load (requests 1-3) and fails every
    /// request afterwards, so a second load fails after a successful first one.
    /// </summary>
    private sealed class FailAfterFirstLoadHandler : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var call = Interlocked.Increment(ref _calls);
            if (call > 3)
            {
                throw new HttpRequestException("cannot reach AI workstation: test failure");
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body =
                path.EndsWith("/documents", StringComparison.Ordinal) ? DocumentsJson
                : path.EndsWith("/history", StringComparison.Ordinal) ? HistoryJson
                : MineJson;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    /// <summary>Serves 12 documents and 12 history items so the 10-item panel cap is observable.</summary>
    private sealed class ManyItemsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var body =
                path.EndsWith("/documents", StringComparison.Ordinal) ? DocumentsManyJson
                : path.EndsWith("/history", StringComparison.Ordinal) ? HistoryManyJson
                : MineEmptyJson;

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                }
            );
        }
    }

    private static readonly string DocumentsManyJson =
        "["
        + string.Join(
            ",",
            Enumerable
                .Range(1, 12)
                .Select(i =>
                    $$"""{"id":"00000000-0000-0000-0000-{{i:D12}}","filename":"doc-{{i}}.pdf","mime":"application/pdf","size":{{i}},"hash":"h{{i}}","status":"Ready","createdBy":"u","createdAt":"2026-09-{{i:D2}}T00:00:00Z"}"""
                )
        )
        + "]";

    private static readonly string HistoryManyJson =
        "{\"items\":["
        + string.Join(
            ",",
            Enumerable
                .Range(1, 12)
                .Select(i =>
                    $$"""{"id":"00000000-0000-0000-0000-{{i:D12}}","promptPreview":"Question {{i}}","answerPreview":"Answer {{i}}","citationCount":1,"latencyMs":20,"createdAt":"2026-09-{{i:D2}}T00:00:00Z"}"""
                )
        )
        + "],\"total\":12,\"limit\":10,\"offset\":0}";

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
