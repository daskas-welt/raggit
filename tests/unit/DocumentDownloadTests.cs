using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 009-library-item-actions US2 (red-first): per-row download fetches original
/// bytes over the authenticated connection and opens them externally; legacy
/// rows without stored bytes surface a clear error without crashing.
/// </summary>
public sealed class DocumentDownloadTests
{
    [Fact]
    public async Task GetContent_ReturnsBytesAndFilename()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var api = new DocumentsApiClient(
            new HttpClient(
                new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pdfBytes)
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") },
                    },
                })
            )
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );

        var (bytes, filename) = await api.GetContentAsync(Guid.NewGuid());

        bytes.Should().Equal(pdfBytes);
        filename.Should().EndWith(".bin");
    }

    [Fact]
    public async Task GetContent_Legacy404_ThrowsClearError()
    {
        var api = new DocumentsApiClient(
            new HttpClient(
                new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":\"original unavailable\"}"),
                })
            )
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );

        var act = () => api.GetContentAsync(Guid.NewGuid());

        (await act.Should().ThrowAsync<HttpRequestException>()).WithMessage(
            "*original unavailable*"
        );
    }

    [Fact]
    public async Task DownloadAndOpen_Success_OpensDownloadedBytes()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var launcher = new FakeLauncher();
        var vm = CreateViewModel(_ => OkBytes(pdfBytes, "application/pdf", "paper.pdf"), launcher);
        var document = TestDocument();

        await vm.DownloadAndOpenCommand.ExecuteAsync(document);

        launcher.Opened.Should().ContainSingle();
        launcher.Opened[0].Bytes.Should().Equal(pdfBytes);
        launcher.Opened[0].Filename.Should().Be("paper.pdf");
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAndOpen_Legacy404_SurfacesErrorWithoutOpening()
    {
        var launcher = new FakeLauncher();
        var vm = CreateViewModel(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"original unavailable\"}"),
            },
            launcher
        );

        await vm.DownloadAndOpenCommand.ExecuteAsync(TestDocument());

        vm.ErrorMessage.Should().Contain("unavailable");
        launcher.Opened.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadAndOpen_NullDocument_DoesNothing()
    {
        var launcher = new FakeLauncher();
        var vm = CreateViewModel(_ => OkBytes(new byte[] { 1 }, "text/plain", "a.txt"), launcher);

        await vm.DownloadAndOpenCommand.ExecuteAsync(null);

        launcher.Opened.Should().BeEmpty();
        vm.ErrorMessage.Should().BeNull();
    }

    private static LibraryViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        ILauncherService launcher
    )
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            preferences: null,
            launcher: launcher
        );
    }

    private static Document TestDocument() =>
        new()
        {
            Id = Guid.NewGuid(),
            Filename = "paper.pdf",
            Mime = DocumentMimeType.Pdf,
            Size = 4,
            Hash = "h",
            Status = DocumentStatus.Ready,
            CreatedBy = "sub",
            CreatedAt = DateTime.UtcNow,
        };

    private static HttpResponseMessage OkBytes(byte[] bytes, string mediaType, string filename)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileName = filename,
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed record OpenedFile(string Filename, byte[] Bytes, string ContentType);

    private sealed class FakeLauncher : ILauncherService
    {
        public List<OpenedFile> Opened { get; } = new();

        public Task OpenAsync(string filename, byte[] content, string contentType)
        {
            Opened.Add(new OpenedFile(filename, content, contentType));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder(request));
    }
}
