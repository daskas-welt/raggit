using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
/// 033 US1: a question that names one supported format auto-produces that file
/// from the grounded answer, saves only where the employee chooses, and never
/// turns an ordinary question or a refusal into a file.
/// </summary>
public sealed class QueryViewModelDocumentTests
{
    [Fact]
    public async Task WordRequest_AutoProducesAndSaves()
    {
        var chunkId = Guid.NewGuid();
        var handler = new DocumentHandler { Answer = AnswerWithCitation(chunkId) };
        var save = new InMemoryFileSaveService { NextPath = "C:\\temp\\report.docx" };
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a Word document about the policy.";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().ContainSingle().Which.Should().Be("docx");
        save.SavedPath.Should().Be("C:\\temp\\report.docx");
        viewModel.StatusMessage.Should().Contain("Saved");
    }

    [Fact]
    public async Task NoFormat_DoesNotProduceAFile()
    {
        var handler = new DocumentHandler { Answer = AnswerWithCitation(Guid.NewGuid()) };
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);

        viewModel.QueryText = "What does the policy say?";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().BeEmpty();
        save.SavedPath.Should().BeNull();
    }

    [Fact]
    public async Task Refusal_DoesNotProduceAFile()
    {
        var handler = new DocumentHandler
        {
            Answer = new QueryStreamEvent
            {
                Type = QueryStreamEvent.AnswerType,
                Answer = "no relevant content found",
                Citations = new List<Citation>(),
                QueryId = Guid.NewGuid(),
            },
        };
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a Word document about something absent.";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().BeEmpty();
        save.SavedPath.Should().BeNull();
    }

    [Fact]
    public async Task CancelledSave_LeavesNoFileAndNoError()
    {
        var handler = new DocumentHandler { Answer = AnswerWithCitation(Guid.NewGuid()) };
        var save = new InMemoryFileSaveService { NextPath = null };
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a PDF about the policy.";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().ContainSingle().Which.Should().Be("pdf");
        save.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().NotContain("failed");
    }

    [Fact]
    public async Task DocumentFailure_ShowsAUserSafeMessage()
    {
        var handler = new DocumentHandler
        {
            Answer = AnswerWithCitation(Guid.NewGuid()),
            DocumentStatus = HttpStatusCode.UnprocessableEntity,
            DocumentError = "no document available",
        };
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a Word document about the policy.";
        await viewModel.AskCommand.ExecuteAsync(null);

        save.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().Contain("no document available");
    }

    [Fact]
    public async Task UnsupportedFormat_ExplainsAndProducesNoFile()
    {
        var handler = new DocumentHandler { Answer = AnswerWithCitation(Guid.NewGuid()) };
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a PowerPoint presentation about the policy.";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().BeEmpty();
        save.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().Contain("Unsupported format");
    }

    [Fact]
    public async Task AmbiguousFormats_AskForAChoiceAndProduceNoFile()
    {
        var handler = new DocumentHandler { Answer = AnswerWithCitation(Guid.NewGuid()) };
        var save = new InMemoryFileSaveService();
        var viewModel = Create(handler, save);

        viewModel.QueryText = "Create a Word and PDF version of the policy.";
        await viewModel.AskCommand.ExecuteAsync(null);

        handler.DocumentFormats.Should().BeEmpty();
        save.SavedPath.Should().BeNull();
        viewModel.StatusMessage.Should().Contain("Choose a format");
    }

    private static QueryStreamEvent AnswerWithCitation(Guid chunkId) =>
        new()
        {
            Type = QueryStreamEvent.AnswerType,
            Answer = $"Customers may return items within 30 days. [{chunkId}]",
            Citations = new List<Citation>
            {
                new()
                {
                    DocumentId = Guid.NewGuid(),
                    DocumentName = "policy.pdf",
                    ChunkId = chunkId,
                    Text = "Customers may return items within 30 days.",
                    Ordinal = 0,
                },
            },
            QueryId = Guid.NewGuid(),
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
        public QueryStreamEvent Answer { get; set; } = new();
        public HttpStatusCode DocumentStatus { get; set; } = HttpStatusCode.OK;
        public string DocumentError { get; set; } = "document unavailable";
        public List<string> DocumentFormats { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/api/queries/stream", StringComparison.Ordinal))
            {
                var line = JsonSerializer.Serialize(
                    Answer,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                );
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(line + "\n", Encoding.UTF8, "application/x-ndjson"),
                };
            }

            if (path.Contains("/documents", StringComparison.Ordinal))
            {
                DocumentFormats.Add(
                    (await request.Content!.ReadAsStringAsync(cancellationToken))
                        .Split("\"format\":\"")
                        .Last()
                        .Split('"')
                        .First()
                );

                if (DocumentStatus != HttpStatusCode.OK)
                {
                    return new HttpResponseMessage(DocumentStatus)
                    {
                        Content = new StringContent(
                            $"{{\"error\":\"{DocumentError}\"}}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    };
                }

                var content = new ByteArrayContent(new byte[] { 1, 2, 3 });
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "report.docx",
                };
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
