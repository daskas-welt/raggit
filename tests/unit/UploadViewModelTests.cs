using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US1: UploadViewModel uses the IFilePicker seam so the
/// picker/upload flow is exercised with no platform UI.
/// </summary>
public sealed class UploadViewModelTests
{
    [Fact]
    public async Task PickFile_SetsSelectedFileName()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(PickedPdf()));

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.SelectedFileName.Should().Be("paper.pdf");
        vm.HasFile.Should().BeTrue();
    }

    [Fact]
    public async Task PickFile_Cancelled_LeavesNoFile()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.SelectedFileName.Should().BeNull();
        vm.HasFile.Should().BeFalse();
    }

    [Fact]
    public async Task PickFile_Failure_SurfacesMessage()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new ThrowingFilePicker(new InvalidOperationException("dialog unavailable"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.SelectedFileName.Should().BeNull();
        vm.StatusMessage.Should().Contain("Could not open file picker");
    }

    [Fact]
    public async Task Upload_Success_SetsStatus()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Uploaded");
        vm.IsUploading.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_UnsupportedType_SurfacesMessage()
    {
        var vm = CreateViewModel(
            _ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"unsupported type: .exe\"}"),
            },
            new FakeFilePicker(PickedPdf())
        );
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("unsupported type");
    }

    [Fact]
    public async Task Upload_NoFile_DoesNothing()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        await vm.UploadCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Be("No file selected.");
    }

    [Fact]
    public async Task CancelUpload_AbortsInFlightRequest()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);

        var uploadTask = vm.UploadCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !vm.IsUploading; i++)
        {
            await Task.Delay(20);
        }
        vm.IsUploading.Should().BeTrue();

        vm.CancelUploadCommand.Execute(null);
        await uploadTask;

        vm.IsUploading.Should().BeFalse();
        vm.StatusMessage.Should().Be("Upload cancelled.");
    }

    [Fact]
    public void CancelUpload_WhenIdle_DoesNothing()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));

        var act = () => vm.CancelUploadCommand.Execute(null);

        act.Should().NotThrow();
        vm.StatusMessage.Should().BeNull();
    }

    private static UploadViewModel CreateViewModel(IFilePicker picker)
    {
        var api = new DocumentsApiClient(
            new HttpClient(new HangingHandler()) { BaseAddress = new Uri("https://w.local/") }
        );
        return new UploadViewModel(api, picker);
    }

    private static UploadViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        IFilePicker picker
    )
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new UploadViewModel(api, picker);
    }

    private static PickedFile PickedPdf() =>
        new("paper.pdf", new MemoryStream(new byte[] { 1, 2, 3 }), "application/pdf");

    private static HttpResponseMessage DocumentsResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        id = Guid.NewGuid(),
                        filename = "paper.pdf",
                        mime = "application/pdf",
                        size = 3,
                        hash = "h",
                        status = "Indexing",
                        createdBy = "sub",
                        createdAt = DateTime.UtcNow,
                    }
                )
            ),
        };

    private sealed class FakeFilePicker : IFilePicker
    {
        private readonly PickedFile? _file;

        public FakeFilePicker(PickedFile? file) => _file = file;

        public Task<PickedFile?> PickAsync() => Task.FromResult(_file);
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Upload_TwiceWithoutRepicking_SendsFullContentBothTimes()
    {
        var sizes = new List<long>();
        var api = new DocumentsApiClient(
            new HttpClient(new ContentCapturingHandler(sizes))
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var vm = new UploadViewModel(api, new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);
        await vm.UploadCommand.ExecuteAsync(null);

        sizes.Should().HaveCount(2);
        sizes[0].Should().BeGreaterThan(0);
        sizes[1].Should().Be(sizes[0]);
    }

    private sealed class ContentCapturingHandler : HttpMessageHandler
    {
        private readonly List<long> _sizes;

        public ContentCapturingHandler(List<long> sizes) => _sizes = sizes;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            _sizes.Add(bytes.Length);
            return DocumentsResponse();
        }
    }

    private sealed class ThrowingFilePicker : IFilePicker
    {
        private readonly Exception _error;

        public ThrowingFilePicker(Exception error) => _error = error;

        public Task<PickedFile?> PickAsync() => Task.FromException<PickedFile?>(_error);
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
