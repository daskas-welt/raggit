using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 034 US1: the page requires a description and an explicit format, then hands
/// the returned file to the existing OS save seam.
/// </summary>
public sealed class DocumentCreationViewModelTests
{
    [Fact]
    public void CreateCommand_RequiresDescriptionAndFormat()
    {
        var viewModel = CreateViewModel(new OkHandler("txt"));

        viewModel.CreateDocumentCommand.CanExecute(null).Should().BeFalse();

        viewModel.Description = "Draft a welcome note.";
        viewModel.CreateDocumentCommand.CanExecute(null).Should().BeFalse();

        viewModel.SelectedFormat = DocumentOutputFormat.Txt;
        viewModel.CreateDocumentCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task CreateCommand_SendsTheSelection_AndSavesTheReturnedFile()
    {
        var fileBytes = Encoding.UTF8.GetBytes("Welcome to the team.");
        var handler = new OkHandler("txt") { Bytes = fileBytes, FileName = "welcome.txt" };
        var save = new InMemoryFileSaveService { NextPath = "C:\\chosen\\welcome.txt" };
        var viewModel = CreateViewModel(handler, save);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);

        handler.RequestBody.Should().Contain("Draft a welcome note.");
        handler.RequestBody.Should().Contain("\"format\":\"txt\"");
        save.SavedPath.Should().Be("C:\\chosen\\welcome.txt");
        viewModel.SavedPath.Should().Be("C:\\chosen\\welcome.txt");
        save.LastFileName.Should().Be("welcome.txt");
        save.LastContent.Should().Equal(fileBytes);
    }

    [Fact]
    public async Task CancelCommand_StopsGeneration_WithoutSavingOrStalePath()
    {
        var handler = new BlockingHandler();
        var save = new InMemoryFileSaveService();
        var viewModel = CreateViewModel(handler, save);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        var running = viewModel.CreateDocumentCommand.ExecuteAsync(null);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.CancelCommand.Execute(null);
        await running.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.IsBusy.Should().BeFalse();
        viewModel.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().BeNull();
        save.SavedPath.Should().BeNull();
    }

    [Fact]
    public async Task CancellingAfterGeneration_DoesNotCallSave()
    {
        var handler = new CancellingHandler();
        var save = new InMemoryFileSaveService();
        var viewModel = CreateViewModel(handler, save);
        handler.Cancel = () => viewModel.CancelCommand.Execute(null);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);

        save.LastContent.Should().BeNull();
        viewModel.SavedPath.Should().BeNull();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCancelled_LeavesNoPathAndNoError()
    {
        var handler = new OkHandler("txt");
        var save = new InMemoryFileSaveService { NextPath = null };
        var viewModel = CreateViewModel(handler, save);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);

        viewModel.SavedPath.Should().BeNull();
        viewModel.HasSavedFile.Should().BeFalse();
        viewModel.StatusMessage.Should().BeNull();
        save.LastContent.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveFailure_ReportsSafeMessageAndNoPath()
    {
        var handler = new OkHandler("txt");
        var save = new InMemoryFileSaveService { FailSave = true };
        var viewModel = CreateViewModel(handler, save);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);

        viewModel.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().Contain("could not be saved");
    }

    [Fact]
    public async Task Busy_CannotStartASecondRequest()
    {
        var handler = new BlockingHandler();
        var viewModel = CreateViewModel(handler);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        var running = viewModel.CreateDocumentCommand.ExecuteAsync(null);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.IsBusy.Should().BeTrue();
        viewModel.CreateDocumentCommand.CanExecute(null).Should().BeFalse();

        viewModel.CancelCommand.Execute(null);
        await running.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailedRequest_ClearsAStaleSavedPath()
    {
        var handler = new OkThenFailHandler();
        var save = new InMemoryFileSaveService { NextPath = "C:\\chosen\\welcome.txt" };
        var viewModel = CreateViewModel(handler, save);
        viewModel.Description = "Draft a welcome note.";
        viewModel.SelectedFormat = DocumentOutputFormat.Txt;

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);
        viewModel.SavedPath.Should().Be("C:\\chosen\\welcome.txt");

        await viewModel.CreateDocumentCommand.ExecuteAsync(null);

        viewModel.SavedPath.Should().BeNull();
        viewModel.HasSavedFile.Should().BeFalse();
        viewModel.StatusMessage.Should().NotBeNull();
    }

    private static DocumentCreationViewModel CreateViewModel(
        HttpMessageHandler handler,
        IFileSaveService? save = null
    )
    {
        var client = new DocumentCreationApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );
        return new DocumentCreationViewModel(client, save ?? new InMemoryFileSaveService());
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class CancellingHandler : HttpMessageHandler
    {
        public Action? Cancel { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Cancel?.Invoke();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Welcome.")),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                "attachment"
            )
            {
                FileNameStar = "welcome.txt",
            };
            return Task.FromResult(response);
        }
    }

    private sealed class OkThenFailHandler : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (Interlocked.Increment(ref _calls) > 1)
            {
                throw new HttpRequestException("AI workstation unavailable");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Welcome.")),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                "attachment"
            )
            {
                FileNameStar = "welcome.txt",
            };
            return Task.FromResult(response);
        }
    }

    private sealed class OkHandler(string format) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public byte[] Bytes { get; init; } = Encoding.UTF8.GetBytes("document");
        public string FileName { get; init; } = "document." + format;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Bytes),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(
                "attachment"
            )
            {
                FileNameStar = FileName,
            };
            return Task.FromResult(response);
        }
    }
}
