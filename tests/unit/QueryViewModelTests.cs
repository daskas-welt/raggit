using System;
using System.Linq;
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
/// 006-client-architecture US2/FR-004/FR-005 + SC-006: the Ask screen is a
/// conversation of ordered person/assistant messages, assistant replies carry
/// the answer (citations tracked alongside), and history is retained in-session.
/// </summary>
public sealed class QueryViewModelTests
{
    [Fact]
    public async Task Ask_AppendsUserThenAssistantMessage()
    {
        var viewModel = CreateViewModel(answer: "grounded answer", citationCount: 2);
        viewModel.QueryText = "what is offline rag?";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[0].IsUser.Should().BeTrue();
        viewModel.Messages[0].Text.Should().Be("what is offline rag?");
        viewModel.Messages[1].IsUser.Should().BeFalse();
        viewModel.Messages[1].Text.Should().Be("grounded answer");
        viewModel.Citations.Should().HaveCount(2);
        viewModel.HasCitations.Should().BeTrue();
    }

    [Fact]
    public async Task Ask_NoRelevantContent_RepliesWithZeroCitations()
    {
        var viewModel = CreateViewModel(answer: "no relevant content found", citationCount: 0);
        viewModel.QueryText = "obscure question";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[1].Text.Should().Be("no relevant content found");
        viewModel.Citations.Should().BeEmpty();
        viewModel.HasCitations.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_Repeated_PreservesAtLeastTwentyMessagesInOrder()
    {
        var viewModel = CreateViewModel(answer: "a", citationCount: 1);

        for (var i = 0; i < 11; i++)
        {
            viewModel.QueryText = $"question {i}";
            await viewModel.AskCommand.ExecuteAsync(null);
        }

        viewModel.Messages.Should().HaveCount(22);
        viewModel
            .Messages.Select(m => m.IsUser)
            .Should()
            .Equal(Enumerable.Range(0, 22).Select(i => i % 2 == 0));
        viewModel.Messages[0].Text.Should().Be("question 0");
        viewModel.Messages[20].Text.Should().Be("question 10");
    }

    [Fact]
    public async Task Ask_OfflineModelUnavailable_SurfacesStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable
        )
        {
            Content = new StringContent("model unavailable offline"),
        });
        var viewModel = new QueryViewModel(
            new QueryApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
            )
        );
        viewModel.QueryText = "q";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("model unavailable offline");
    }

    private static QueryViewModel CreateViewModel(string answer, int citationCount)
    {
        var citations = Enumerable
            .Range(0, citationCount)
            .Select(i => new
            {
                documentId = Guid.NewGuid(),
                chunkId = Guid.NewGuid(),
                text = $"quote {i}",
                ordinal = i,
            })
            .ToArray();

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        answer,
                        citations,
                        retrievedChunkIds = Array.Empty<Guid>(),
                        latencyMs = 7,
                    }
                )
            ),
        });
        var apiClient = new QueryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );
        return new QueryViewModel(apiClient);
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
