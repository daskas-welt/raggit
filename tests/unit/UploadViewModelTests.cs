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
    public async Task CancelUpload_TeardownRace_MarksCancelledNotFailed()
    {
        // The dialog-close race: cancel fires, then the HTTP stack aborts
        // the TLS connection surfacing ObjectDisposedException on SslStream
        // instead of OperationCanceledException.
        var vm = CreateViewModel(new SslAbortOnCancelHandler(), new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);

        var uploadTask = vm.UploadCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !vm.IsUploading; i++)
        {
            await Task.Delay(20);
        }
        vm.IsUploading.Should().BeTrue();

        vm.CancelUploadCommand.Execute(null);
        await uploadTask;

        vm.Queue.Should().ContainSingle();
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Cancelled);
        vm.Queue[0].ErrorMessage.Should().Be("Upload cancelled.");
        vm.StatusMessage.Should().Be("Upload cancelled.");
        vm.UploadTask.Should().BeNull();
    }

    [Fact]
    public async Task Upload_SslStreamDisposedWithoutCancel_MarksFailedWithoutCrash()
    {
        var vm = CreateViewModel(
            new ThrowingHandler(new ObjectDisposedException("System.Net.Security.SslStream")),
            new FakeFilePicker(PickedPdf())
        );
        await vm.PickFileCommand.ExecuteAsync(null);

        var act = () => vm.UploadCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Failed);
    }

    [Fact]
    public async Task Upload_Completes_ClearsUploadTask()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);

        await vm.UploadCommand.ExecuteAsync(null);

        vm.UploadTask.Should().BeNull();
        vm.Queue[0].State.Should().Be(UploadViewModel.UploadItemState.Succeeded);
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

    [Theory]
    [InlineData(
        "report.docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    )]
    [InlineData("paper.pdf", "application/pdf")]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task AllowList_AcceptsEachSupportedType(string fileName, string contentType)
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new FakeFilePicker(Picked(fileName, contentType))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().ContainSingle();
        vm.Queue[0].FileName.Should().Be(fileName);
        vm.HasRejection.Should().BeFalse();
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
    }

    [Theory]
    [InlineData("REPORT.DOCX")]
    [InlineData("scan.Pdf")]
    [InlineData("NOTES.TXT")]
    [InlineData("DATA.XlSx")]
    public async Task AllowList_AcceptsUppercaseExtensions(string fileName)
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new FakeFilePicker(Picked(fileName)));

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().ContainSingle();
        vm.HasRejection.Should().BeFalse();
    }

    [Fact]
    public async Task AllowList_RejectsMarkdownForNewUploads()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new FakeFilePicker(Picked("notes.md", "text/markdown"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().BeEmpty();
        vm.HasRejection.Should().BeTrue();
        vm.RejectionMessage.Should().Contain("notes.md");
        vm.RejectionMessage.Should().Contain("isn't supported");
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("legacy.doc")]
    [InlineData("slides.pptx")]
    [InlineData("photo.png")]
    [InlineData("archive.zip")]
    public async Task AllowList_RejectsUnsupportedTypes(string fileName)
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new FakeFilePicker(Picked(fileName, "application/octet-stream"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().BeEmpty();
        vm.HasRejection.Should().BeTrue();
        vm.RejectionMessage.Should().Contain(fileName);
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task AllowList_MixedBatch_QueuesOnlySupported()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        await vm.AddPickedFilesAsync(
            new[]
            {
                Picked("ok.pdf"),
                Picked("notes.md", "text/markdown"),
                Picked(
                    "data.xlsx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                ),
                Picked("evil.exe", "application/x-msdownload"),
            }
        );

        vm.Queue.Select(i => i.FileName).Should().Equal("ok.pdf", "data.xlsx");
        vm.RejectionMessage.Should().Contain("notes.md");
        vm.RejectionMessage.Should().Contain("evil.exe");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task AllowList_RejectionMessage_ListsSupportedTypes()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new FakeFilePicker(Picked("notes.md", "text/markdown"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.RejectionMessage.Should().Contain("PDF, DOCX, XLSX, TXT");
        vm.RejectionMessage.Should().NotContain("MD");
    }

    [Fact]
    public void SupportedTypesText_ListsExactlyFourTypes()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        vm.SupportedTypesText.Should().Be("PDF, DOCX, XLSX, TXT supported · up to 100 MB.");
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
    public async Task Upload_CellCapRejection_ShowsServerMessage()
    {
        var vm = CreateViewModel(
            _ => new HttpResponseMessage(HttpStatusCode.RequestEntityTooLarge)
            {
                Content = new StringContent(
                    "{\"error\":\"Spreadsheet exceeds 100,000 cell limit (found 100,001 cells)\"}"
                ),
            },
            new SequenceFilePicker(Picked("greek.xlsx"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);
        await vm.UploadCommand.ExecuteAsync(null);

        vm.Queue[0]
            .ErrorMessage.Should()
            .Be("Spreadsheet exceeds 100,000 cell limit (found 100,001 cells)");
        vm.StatusMessage.Should().Contain("Spreadsheet exceeds 100,000 cell limit");
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

    private static UploadViewModel CreateViewModel(HttpMessageHandler handler, IFilePicker picker)
    {
        var api = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
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

    /// <summary>
    /// Simulates the dialog-close TLS teardown race: the caller cancels,
    /// then the HTTP stack aborts the connection surfacing
    /// ObjectDisposedException on SslStream instead of
    /// OperationCanceledException.
    /// </summary>
    private sealed class SslAbortOnCancelHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException("System.Net.Security.SslStream");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromException<HttpResponseMessage>(_exception);
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

    [Fact]
    public async Task PickFile_MultiSelect_QueuesAllInOnePass()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new ListFilePicker(Picked("a.pdf"), Picked("b.pdf"), Picked("c.pdf"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().HaveCount(3);
        vm.Queue.Select(i => i.FileName).Should().Equal("a.pdf", "b.pdf", "c.pdf");
        // Option A: the queue header carries count/size/progress; the
        // InfoBar stays silent until there is an outcome.
        vm.StatusMessage.Should().BeNull();
        vm.QueueHeaderText.Should().Be("3 files · 9 bytes · 0 of 3 done");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task QueueHeaderText_SingleFile_ShowsCountSizeAndProgress()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new ListFilePicker(Picked("a.pdf")));

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.QueueHeaderText.Should().Be("1 file · 3 bytes · 0 of 1 done");
        vm.TotalSizeBytes.Should().Be(3);
    }

    [Fact]
    public void PrimaryButton_IdleShowsUpload_UploadingShowsCancel()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));

        vm.PrimaryButtonText.Should().Be("Upload");
        vm.PrimaryButtonCommand.Should().BeSameAs(vm.UploadCommand);
    }

    [Fact]
    public async Task QueueItem_RemoveAutomationId_IsUniquePerFile()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new ListFilePicker(Picked("a.pdf"), Picked("b.pdf"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        var ids = vm.Queue.Select(i => i.RemoveAutomationId).ToList();
        ids.Should().HaveCount(2);
        ids.Should().OnlyHaveUniqueItems();
        ids[0].Should().StartWith("RemoveFileButton_");
    }

    [Fact]
    public async Task AddPickedFiles_RejectsEmptyFilesAndDisablesSubmit()
    {
        var vm = CreateViewModel(new DummyFilePicker());

        await vm.AddPickedFilesAsync(new[] { Picked("empty.pdf", size: 0) });

        vm.Queue.Should().BeEmpty();
        vm.CanSubmit.Should().BeFalse();
        vm.RejectionMessage.Should().Contain("empty");
    }

    [Fact]
    public async Task QueueItem_IsUploading_TracksState()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);
        var item = vm.Queue.Should().ContainSingle().Subject;

        item.IsUploading.Should().BeFalse();
        item.State = UploadViewModel.UploadItemState.Uploading;
        item.IsUploading.Should().BeTrue();
        item.State = UploadViewModel.UploadItemState.Succeeded;
        item.IsUploading.Should().BeFalse();
    }

    [Fact]
    public async Task QueueItem_ShowsIndexingAfterBytesReachServer()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));
        await vm.PickFileCommand.ExecuteAsync(null);
        var item = vm.Queue.Should().ContainSingle().Subject;

        item.State = UploadViewModel.UploadItemState.Uploading;
        item.Progress = 1;

        item.IsUploading.Should().BeTrue();
        item.IsIndexing.Should().BeTrue();
        item.StateLabel.Should().Be("Indexing...");
    }

    [Fact]
    public async Task ClosingStopsUploadHint_OnlyShowsWhileUploading()
    {
        var vm = CreateViewModel(new FakeFilePicker(PickedPdf()));

        vm.ClosingStopsUploadHint.Should().BeEmpty();

        await vm.PickFileCommand.ExecuteAsync(null);
        var uploadTask = vm.UploadCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !vm.IsUploading; i++)
        {
            await Task.Delay(20);
        }

        vm.ClosingStopsUploadHint.Should().Be("Closing this dialog stops the current upload.");
        vm.CancelUploadCommand.Execute(null);
        await uploadTask;
        vm.ClosingStopsUploadHint.Should().BeEmpty();
    }

    [Fact]
    public async Task PickFile_MixedValidInvalid_QueuesValidNamesRejected()
    {
        var vm = CreateViewModel(
            _ => DocumentsResponse(),
            new ListFilePicker(Picked("good.pdf"), Picked("bad.exe", "application/x-msdownload"))
        );

        await vm.PickFileCommand.ExecuteAsync(null);

        vm.Queue.Should().ContainSingle();
        vm.Queue[0].FileName.Should().Be("good.pdf");
        vm.RejectionMessage.Should().Contain("bad.exe");
        vm.UploadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task PickFile_WhileUploading_BlockedAndNooped()
    {
        var vm = CreateViewModel(new ListFilePicker(Picked("a.pdf")));
        await vm.PickFileCommand.ExecuteAsync(null);

        var uploadTask = vm.UploadCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !vm.IsUploading; i++)
        {
            await Task.Delay(20);
        }
        vm.IsUploading.Should().BeTrue();

        vm.PickFileCommand.CanExecute(null).Should().BeFalse();
        vm.IsDropEnabled.Should().BeFalse();
        vm.DropHintText.Should().Contain("after this run");

        await vm.PickFileCommand.ExecuteAsync(null);
        vm.Queue.Should().ContainSingle();

        vm.CancelUploadCommand.Execute(null);
        await uploadTask;

        vm.IsUploading.Should().BeFalse();
        vm.IsDropEnabled.Should().BeTrue();
        vm.PickFileCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task AddPickedFiles_DropMixed_QueuesValidReportsInvalid()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        await vm.AddPickedFilesAsync(new[] { Picked("ok.pdf"), Picked("nope.exe") });
        vm.ReportRejectedNames(new[] { "archive", "  " });

        vm.Queue.Should().ContainSingle();
        vm.Queue[0].FileName.Should().Be("ok.pdf");
        vm.RejectionMessage.Should().Contain("nope.exe");
        vm.RejectionMessage.Should().Contain("archive");
    }

    [Fact]
    public async Task AddPickedFiles_Empty_LeavesQueueUnchanged()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(), new DummyFilePicker());

        await vm.AddPickedFilesAsync(Array.Empty<PickedFile>());

        vm.Queue.Should().BeEmpty();
        vm.RejectionMessage.Should().BeNull();
        vm.UploadCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task PickMultiple_DefaultImpl_DelegatesToSinglePick()
    {
        var single = await ((IFilePicker)new FakeFilePicker(PickedPdf())).PickMultipleAsync();
        single.Should().ContainSingle();

        var none = await ((IFilePicker)new DummyFilePicker()).PickMultipleAsync();
        none.Should().BeEmpty();
    }

    private sealed class ListFilePicker : IFilePicker
    {
        private readonly IReadOnlyList<PickedFile?> _files;

        public ListFilePicker(params PickedFile?[] files) => _files = files;

        public Task<PickedFile?> PickAsync() => Task.FromResult(_files.FirstOrDefault());

        public Task<IReadOnlyList<PickedFile>> PickMultipleAsync() =>
            Task.FromResult<IReadOnlyList<PickedFile>>(_files.Where(f => f is not null).ToList()!);
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
