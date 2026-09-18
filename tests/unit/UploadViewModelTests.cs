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
using RAGGit.Core.Models;
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

    [Fact]
    public async Task PickFile_AppendsToQueue_WithNameAndSize()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(PickedPdf()));

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().HaveCount(1);
        vm.Queue[0].FileName.Should().Be("paper.pdf");
        vm.Queue[0].SizeBytes.Should().Be(3);
        vm.HasFiles.Should().BeTrue();
        vm.TotalCount.Should().Be(1);
        vm.CompletedCountText.Should().Be("0 of 1 done");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
        vm.SupportedTypesText.Should().Contain("PDF");
        vm.IsAllSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task PickFile_SecondPick_AppendsQueue()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new SequenceFilePicker(Picked("a.pdf"), Picked("b.pdf"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().HaveCount(2);
        vm.Queue.Select(i => i.FileName).Should().Equal("a.pdf", "b.pdf");
        vm.CompletedCountText.Should().Be("0 of 2 done");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task PickFile_UnsupportedType_BlockedWithRejection()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new FakeFilePicker(Picked("evil.exe", "application/x-msdownload"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().BeEmpty();
        vm.HasRejection.Should().BeTrue();
        vm.RejectionMessage.Should().Contain("isn't supported");
        vm.RejectionMessage.Should().Contain("evil.exe");
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
        vm.HasFiles.Should().BeFalse();
    }

    [Fact]
    public async Task PickFile_Oversize_BlockedWithRejection()
    {
        var oversized = new PickedFile(
            "big.pdf",
            new SizedStream(DocumentValidation.MaxFileSizeBytes + 1),
            "application/pdf"
        );
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(oversized));

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().BeEmpty();
        vm.HasRejection.Should().BeTrue();
        vm.RejectionMessage.Should().Contain("too large");
        vm.RejectionMessage.Should().Contain("100 MB");
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Upload_Gating_DisabledUntilValidFileQueued()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_MultiFile_PerFileProgressAndCompletedCount()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new SequenceFilePicker(Picked("a.pdf"), Picked("b.pdf"))
        );
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);

        vm.Queue.Should().HaveCount(2);
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.Queue[1].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.Queue[0].Progress.Should().Be(1);
        vm.Queue[1].Progress.Should().Be(1);
        vm.CompletedCount.Should().Be(2);
        vm.CompletedCountText.Should().Be("2 of 2 done");
        vm.IsAllSucceeded.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Uploaded");
        vm.StatusSeverity.Should().Be("Success");
    }

    [Fact]
    public async Task Upload_PartialFailure_StaysOpenWithPerFileOutcomes()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(DocumentsResponse());
        responses.Enqueue(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"unsupported type: .exe\"}"),
            }
        );
        var vm = CreateViewModel(
            _ => responses.Dequeue(),
            new SequenceFilePicker(Picked("a.pdf"), Picked("b.pdf"))
        );
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);

        vm.IsUploading.Should().BeFalse();
        vm.IsAllSucceeded.Should().BeFalse();
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.Queue[1].State.Should().Be(UploadViewModel.UploadItemState.Failed);
        vm.Queue[1].ErrorMessage.Should().Contain("unsupported type");
        vm.CompletedCount.Should().Be(1);
        vm.CompletedCountText.Should().Be("1 of 2 done");
        vm.HasStatus.Should().BeTrue();
        vm.StatusMessage.Should().Contain("1 of 2");
        vm.StatusSeverity.Should().Be("Error");
    }

    [Fact]
    public async Task CancelUpload_MarksCurrentItemCancelled()
    {
        var vm = CreateViewModel(new SequenceFilePicker(PickedPdf()));
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
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Cancelled);
        vm.StatusMessage.Should().Be("Upload cancelled.");
        vm.IsAllSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveFile_RemovesQueuedEntry_UpdatesGating()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new SequenceFilePicker(Picked("a.pdf"), Picked("b.pdf"))
        );
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.PickFileCommand.ExecuteAsync(null);

        vm.RemoveFileCommand.Execute(vm.Queue[0]);

        vm.Queue.Should().HaveCount(1);
        vm.Queue[0].FileName.Should().Be("b.pdf");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();

        vm.RemoveFileCommand.Execute(vm.Queue[0]);

        vm.Queue.Should().BeEmpty();
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
        vm.SelectedFileName.Should().BeNull();
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

    private static PickedFile Picked(
        string fileName,
        string contentType = "application/pdf",
        int size = 3
    ) => new(fileName, new MemoryStream(CreateBytes(size)), contentType);

    private static byte[] CreateBytes(int size)
    {
        var bytes = new byte[size];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 251);
        }
        return bytes;
    }

    private sealed class SequenceFilePicker : IFilePicker
    {
        private readonly Queue<PickedFile?> _files;

        public SequenceFilePicker(params PickedFile?[] files) =>
            _files = new Queue<PickedFile?>(files);

        public Task<PickedFile?> PickAsync() =>
            Task.FromResult(_files.Count > 0 ? _files.Dequeue() : null);
    }

    /// <summary>
    /// Seekable zero-filled stream of an arbitrary logical length without
    /// allocating the backing buffer (for oversize pick-time tests).
    /// </summary>
    private sealed class SizedStream : Stream
    {
        private readonly long _length;
        private long _position;

        public SizedStream(long length) => _length = length;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _length - _position;
            if (remaining <= 0)
            {
                return 0;
            }
            var take = (int)Math.Min(count, remaining);
            Array.Clear(buffer, offset, take);
            _position += take;
            return take;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => _position,
            };

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

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
    public async Task Upload_AfterFullSuccess_DoesNotReupload()
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

        // A retry after full success must not create a duplicate document.
        sizes.Should().HaveCount(1);
        sizes[0].Should().BeGreaterThan(0);
        vm.IsAllSucceeded.Should().BeTrue();
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_RetryAfterPartialFailure_OnlyRetriesFailed()
    {
        var sizes = new List<long>();
        var queue = new Queue<HttpResponseMessage>();
        queue.Enqueue(DocumentsResponse());
        queue.Enqueue(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"unsupported type: .exe\"}"),
            }
        );
        queue.Enqueue(DocumentsResponse());
        var api = new DocumentsApiClient(
            new HttpClient(new SequencedHandler(queue, sizes))
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var vm = new UploadViewModel(api, new SequenceFilePicker(Picked("a.pdf"), Picked("b.pdf")));
        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.Queue[1].State.Should().Be(UploadViewModel.UploadItemState.Failed);
        vm.UploadCommand.CanExecute(null).Should().BeTrue();

        await vm.UploadCommand.ExecuteAsync(null);

        // Only the failed item is retried (full content, rewound — never 0
        // bytes); the succeeded item is left alone, so no duplicate is made.
        sizes.Should().HaveCount(3);
        sizes.Should().OnlyContain(s => s > 0);
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.Queue[1].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
        vm.IsAllSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_Gating_AfterRun_ReflectsRetryability()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new SequenceFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);
        vm.UploadCommand.CanExecute(null).Should().BeTrue();

        await vm.UploadCommand.ExecuteAsync(null);

        // Nothing retryable remains once everything succeeded.
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        private readonly List<long> _sizes;

        public SequencedHandler(Queue<HttpResponseMessage> responses, List<long> sizes)
        {
            _responses = responses;
            _sizes = sizes;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            _sizes.Add(bytes.Length);
            return _responses.Dequeue();
        }
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
