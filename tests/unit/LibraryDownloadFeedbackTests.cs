using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
/// 028-pages-ux-polish Phase 4 (US2, red-first): per-document download busy
/// gating and outcome notifications on <see cref="LibraryViewModel"/>.
/// </summary>
public sealed class LibraryDownloadFeedbackTests
{
    [Fact]
    public async Task DownloadInFlight_BusySetContainsExactlyTheDocumentId()
    {
        var gate = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var notifications = new InMemoryNotificationService();
        var vm = CreateViewModel(_ => gate.Task, notifications);
        var document = TestDocument("paper.pdf");
        var other = TestDocument("other.pdf");

        var run = vm.DownloadAndOpenCommand.ExecuteAsync(document);

        await WaitUntilAsync(() => vm.DownloadingIds.Contains(document.Id));
        vm.DownloadingIds.Should().ContainSingle().Which.Should().Be(document.Id);
        vm.IsDownloading(document.Id).Should().BeTrue();
        vm.IsDownloading(other.Id).Should().BeFalse("other rows stay actionable");

        gate.SetResult(OkBytes(new byte[] { 0x25, 0x50, 0x44, 0x46 }, "paper.pdf"));
        await run;

        vm.DownloadingIds.Should().BeEmpty("the busy set always empties");
    }

    [Fact]
    public async Task DownloadSuccess_RaisesExactlyOneSuccessNotification()
    {
        var notifications = new InMemoryNotificationService();
        var launcher = new InMemoryLauncherService();
        var vm = CreateViewModel(
            _ => Task.FromResult(OkBytes(new byte[] { 0x25, 0x50 }, "paper.pdf")),
            notifications,
            launcher
        );

        await vm.DownloadAndOpenCommand.ExecuteAsync(TestDocument("paper.pdf"));

        launcher.Opened.Should().ContainSingle();
        notifications.Shown.Should().ContainSingle();
        notifications.Shown[0].Kind.Should().Be(NotificationKind.Success);
        notifications.Shown[0].Title.Should().Be("Downloaded");
        notifications.Shown[0].Message.Should().Contain("paper.pdf");
        vm.ErrorMessage.Should().BeNull();
        vm.DownloadingIds.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadFailure_RaisesExactlyOneDangerNotification()
    {
        var notifications = new InMemoryNotificationService();
        var launcher = new InMemoryLauncherService();
        var vm = CreateViewModel(
            _ =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("{\"error\":\"original unavailable\"}"),
                    }
                ),
            notifications,
            launcher
        );

        await vm.DownloadAndOpenCommand.ExecuteAsync(TestDocument("paper.pdf"));

        launcher.Opened.Should().BeEmpty();
        vm.ErrorMessage.Should().Contain("unavailable");
        notifications.Shown.Should().ContainSingle();
        notifications.Shown[0].Kind.Should().Be(NotificationKind.Danger);
        vm.DownloadingIds.Should().BeEmpty("the busy set always empties, even on failure");
    }

    private static LibraryViewModel CreateViewModel(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder,
        INotificationService notifications,
        ILauncherService? launcher = null
    )
    {
        var api = new DocumentsApiClient(
            new HttpClient(new GateHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            launcher ?? new InMemoryLauncherService(),
            preferences: null,
            notifications: notifications
        );
    }

    private static Document TestDocument(string filename) =>
        new()
        {
            Id = Guid.NewGuid(),
            Filename = filename,
            Mime = DocumentMimeType.Pdf,
            Size = 4,
            Hash = "h",
            Status = DocumentStatus.Ready,
            CreatedBy = "sub",
            CreatedAt = DateTime.UtcNow,
        };

    private static HttpResponseMessage OkBytes(byte[] bytes, string filename)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileName = filename,
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class GateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public GateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => _responder(request);
    }
}
