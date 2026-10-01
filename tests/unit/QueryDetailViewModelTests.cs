using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US1: QueryDetailViewModel loads full answer +
/// ordered citations with no UI shell, and surfaces a not-owned/not-found
/// response as an error instead of data.
/// </summary>
public sealed class QueryDetailViewModelTests
{
    [Fact]
    public async Task LoadDetail_SetsDetailWithCitations()
    {
        var id = Guid.NewGuid();
        var vm = CreateViewModel(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        id,
                        prompt = "q",
                        answer = "full answer",
                        citations = new[]
                        {
                            new
                            {
                                documentId = Guid.NewGuid(),
                                chunkId = Guid.NewGuid(),
                                text = "quote",
                                ordinal = 0,
                            },
                        },
                        latencyMs = 12,
                        createdAt = DateTime.UtcNow,
                    }
                )
            ),
        });

        await vm.LoadDetailCommand.ExecuteAsync(id);

        vm.Detail.Should().NotBeNull();
        vm.Detail!.Answer.Should().Be("full answer");
        vm.Detail.Citations.Should().HaveCount(1);
        vm.ErrorMessage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadDetail_NotFound_SetsErrorMessageAndNoDetail()
    {
        var vm = CreateViewModel(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found"),
        });

        await vm.LoadDetailCommand.ExecuteAsync(Guid.NewGuid());

        vm.Detail.Should().BeNull();
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    private static QueryDetailViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder
    )
    {
        var api = new QueryHistoryApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new QueryDetailViewModel(api);
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
