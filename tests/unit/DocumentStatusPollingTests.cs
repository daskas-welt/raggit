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
/// Both tables refresh their rows' status while ingest is still in flight and
/// stop once everything is terminal (Library + My Documents).
/// </summary>
public sealed class DocumentStatusPollingTests
{
    private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task Library_PollsUntilTerminal_ThenStops()
    {
        var calls = 0;
        var vm = CreateLibrary(responder: () =>
        {
            var n = Interlocked.Increment(ref calls);
            return LibraryResponse(n == 1 ? "Indexing" : "Ready");
        });

        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.Documents.Should().ContainSingle();
        vm.Documents[0].Status.Should().Be(DocumentStatus.Indexing);

        await WaitUntilAsync(() => vm.Documents[0].Status == DocumentStatus.Ready);
        await WaitUntilAsync(() => Volatile.Read(ref calls) >= 2);

        var settled = Volatile.Read(ref calls);
        await Task.Delay(80);
        Volatile.Read(ref calls).Should().Be(settled);
    }

    [Fact]
    public async Task Library_TerminalOnLoad_DoesNotPoll()
    {
        var calls = 0;
        var vm = CreateLibrary(responder: () =>
        {
            Interlocked.Increment(ref calls);
            return LibraryResponse("Ready");
        });

        await vm.LoadDocumentsCommand.ExecuteAsync(null);
        await Task.Delay(80);

        Volatile.Read(ref calls).Should().Be(1);
    }

    [Fact]
    public async Task Mine_PollsUntilTerminal_ThenStops()
    {
        var calls = 0;
        var vm = CreateMine(responder: () =>
        {
            var n = Interlocked.Increment(ref calls);
            return MineResponse(n == 1 ? "Indexing" : "Ready");
        });

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Items.Should().ContainSingle();
        vm.Items[0].Status.Should().Be("Indexing");

        await WaitUntilAsync(() => vm.Items[0].Status == "Ready");
        await WaitUntilAsync(() => Volatile.Read(ref calls) >= 2);

        var settled = Volatile.Read(ref calls);
        await Task.Delay(80);
        Volatile.Read(ref calls).Should().Be(settled);
    }

    private static LibraryViewModel CreateLibrary(Func<HttpResponseMessage> responder)
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            new InMemoryLauncherService(),
            statusPollInterval: FastInterval
        );
    }

    private static DocumentsMineViewModel CreateMine(Func<HttpResponseMessage> responder)
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new DocumentsMineViewModel(api, statusPollInterval: FastInterval);
    }

    private static HttpResponseMessage LibraryResponse(string status)
    {
        var body = JsonSerializer.Serialize(
            new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    filename = "paper.pdf",
                    mime = "application/pdf",
                    size = 3,
                    hash = "h",
                    status,
                    createdBy = "sub",
                    createdAt = DateTime.UtcNow,
                },
            }
        );
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private static HttpResponseMessage MineResponse(string status)
    {
        var body = JsonSerializer.Serialize(
            new
            {
                items = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(),
                        filename = "paper.pdf",
                        size = 3,
                        status,
                        createdAt = DateTime.UtcNow,
                    },
                },
                total = 1,
                limit = 20,
                offset = 0,
            }
        );
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responder;

        public StubHandler(Func<HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder());
    }
}
