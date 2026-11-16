using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Models;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US3: any eligible message can be saved in each format, and a saved file
/// can be opened again.
/// </summary>
public sealed class QueryViewModelSaveAsTests
{
    [Fact]
    public async Task SaveAs_RequestsTheChosenFormatAndRecordsThePath()
    {
        var handler = new DocumentHandler();
        var save = new InMemoryFileSaveService { NextPath = "C:\\temp\\answer.pdf" };
        var viewModel = Create(handler, save);
        var message = Message();

        await viewModel.SaveDocumentCommand.ExecuteAsync(
            new DocumentSaveRequest(message, DocumentOutputFormat.Pdf)
        );

        handler.Formats.Should().ContainSingle().Which.Should().Be("pdf");
        message.SavedPath.Should().Be("C:\\temp\\answer.pdf");
        viewModel.StatusMessage.Should().Contain("Saved");
    }

    [Fact]
    public async Task Open_LaunchesTheSavedPath()
    {
        var handler = new DocumentHandler();
        var save = new InMemoryFileSaveService { NextPath = "C:\\temp\\answer.docx" };
        var viewModel = Create(handler, save);
        var message = Message();

        await viewModel.SaveDocumentCommand.ExecuteAsync(
            new DocumentSaveRequest(message, DocumentOutputFormat.Docx)
        );
        await viewModel.OpenDocumentCommand.ExecuteAsync(message);

        save.OpenedPath.Should().Be("C:\\temp\\answer.docx");
    }

    [Fact]
    public async Task RefusalMessage_IsNotSaved()
    {
        var handler = new DocumentHandler();
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);
        var message = new ChatMessage
        {
            Text = "no relevant content found",
            IsUser = false,
            QueryId = Guid.NewGuid(),
        };

        await viewModel.SaveDocumentCommand.ExecuteAsync(
            new DocumentSaveRequest(message, DocumentOutputFormat.Txt)
        );

        handler.Formats.Should().BeEmpty();
        save.SavedPath.Should().BeNull();
    }

    private static ChatMessage Message() =>
        new()
        {
            Text = "An answer [1].",
            IsUser = false,
            QueryId = Guid.NewGuid(),
            Citations = new[]
            {
                new Citation
                {
                    DocumentId = Guid.NewGuid(),
                    ChunkId = Guid.NewGuid(),
                    Text = "quote",
                    Ordinal = 0,
                },
            },
        };

    private static QueryViewModel Create(DocumentHandler handler, IFileSaveService save)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") };
        return new QueryViewModel(
            new QueryApiClient(http),
            new QueryHistoryApiClient(http),
            new ClientSession(),
            new ConversationStore(),
            new DocumentApiClient(http),
            save
        );
    }

    private sealed class DocumentHandler : HttpMessageHandler
    {
        public System.Collections.Generic.List<string> Formats { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            if (!request.RequestUri!.AbsolutePath.Contains("/documents", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Formats.Add(body.Split("\"format\":\"").Last().Split('"').First());

            var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "answer.pdf",
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
