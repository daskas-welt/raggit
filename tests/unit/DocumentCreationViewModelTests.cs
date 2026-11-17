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
        var handler = new OkHandler("txt")
        {
            Bytes = fileBytes,
            FileName = "welcome.txt",
        };
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
