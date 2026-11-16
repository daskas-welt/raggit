using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>033 US3: an answer reopened from History can be saved as a document.</summary>
public sealed class QueryDetailViewModelDocumentTests
{
    [Fact]
    public async Task SaveDocument_UsesTheDetailIdAndRecordsThePath()
    {
        var handler = new DocumentHandler();
        var save = new InMemoryFileSaveService { NextPath = "C:\\temp\\history.docx" };
        var viewModel = Create(handler, save);
        viewModel.Detail = Detail();

        await viewModel.SaveDocumentCommand.ExecuteAsync(DocumentOutputFormat.Docx);

        handler.Formats.Should().ContainSingle().Which.Should().Be("docx");
        viewModel.SavedPath.Should().Be("C:\\temp\\history.docx");
        viewModel.DocumentStatus.Should().Contain("Saved");
    }

    [Fact]
    public async Task Open_LaunchesTheSavedPath()
    {
        var handler = new DocumentHandler();
        var save = new InMemoryFileSaveService { NextPath = "C:\\temp\\history.docx" };
        var viewModel = Create(handler, save);
        viewModel.Detail = Detail();

        await viewModel.SaveDocumentCommand.ExecuteAsync(DocumentOutputFormat.Docx);
        await viewModel.OpenDocumentCommand.ExecuteAsync(null);

        save.OpenedPath.Should().Be("C:\\temp\\history.docx");
    }

    private static QueryDetail Detail() =>
        new()
        {
            Id = Guid.NewGuid(),
            Prompt = "Create a Word document about the policy.",
            Answer = "The policy allows returns [1].",
            Citations = new System.Collections.Generic.List<HistoryCitation>
            {
                new()
                {
                    DocumentId = Guid.NewGuid(),
                    ChunkId = Guid.NewGuid(),
                    Text = "quote",
                    Ordinal = 0,
                },
            },
            LatencyMs = 1,
            CreatedAt = DateTime.UtcNow,
        };

    private static QueryDetailViewModel Create(DocumentHandler handler, IFileSaveService save)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") };
        return new QueryDetailViewModel(
            new QueryHistoryApiClient(http),
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
            content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            );
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "history.docx",
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
