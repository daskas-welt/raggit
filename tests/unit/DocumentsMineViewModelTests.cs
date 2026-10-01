using System;
using System.Linq;
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
/// T028: DocumentsMineViewModel first page, LoadMore, refresh, empty state.
/// </summary>
public sealed class DocumentsMineViewModelTests
{
    [Fact]
    public async Task Load_FirstPage_PopulatesItemsAndTotal()
    {
        var viewModel = CreateViewModel(total: 3, pageSize: 2);

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.Total.Should().Be(3);
        viewModel.Items.Should().HaveCount(2);
        viewModel.IsEmpty.Should().BeFalse();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadMore_AppendsSecondPage()
    {
        var viewModel = CreateViewModel(total: 3, pageSize: 2);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        viewModel.Items.Should().HaveCount(3);
        viewModel
            .Items.Select(i => i.Filename)
            .Should()
            .ContainInOrder("doc 0.txt", "doc 1.txt", "doc 2.txt");
    }

    [Fact]
    public async Task Refresh_ResetsToFirstPage()
    {
        var viewModel = CreateViewModel(total: 3, pageSize: 2);
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadMoreCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Items.Should().HaveCount(2);
        viewModel.Items.Select(i => i.Filename).Should().ContainInOrder("doc 0.txt", "doc 1.txt");
    }

    [Fact]
    public async Task Load_NoDocuments_SetsEmpty()
    {
        var viewModel = CreateViewModel(total: 0, pageSize: 2);

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.Total.Should().Be(0);
        viewModel.Items.Should().BeEmpty();
        viewModel.IsEmpty.Should().BeTrue();
    }

    private static DocumentsMineViewModel CreateViewModel(int total, int pageSize)
    {
        var viewModel = BuildViewModel(total, pageSize);
        viewModel.Limit = pageSize;
        return viewModel;
    }

    private static DocumentsMineViewModel BuildViewModel(int total, int pageSize)
    {
        var handler = new TestMessageHandler(request =>
        {
            request.RequestUri!.PathAndQuery.Should().StartWith("/api/documents/mine");
            var (limit, offset) = ParsePaging(request.RequestUri!.Query, pageSize);
            var items = Enumerable
                .Range(offset, Math.Max(0, Math.Min(limit, total - offset)))
                .Select(i => new
                {
                    id = Guid.NewGuid(),
                    filename = $"doc {i}.txt",
                    size = 128,
                    status = "Ready",
                    createdAt = DateTime.UtcNow,
                })
                .ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            items,
                            total,
                            limit,
                            offset,
                        }
                    )
                ),
            };
        });
        var apiClient = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") }
        );
        return new DocumentsMineViewModel(apiClient);
    }

    private static (int Limit, int Offset) ParsePaging(string query, int defaultLimit)
    {
        var limit = defaultLimit;
        var offset = 0;
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[0] == "limit" && int.TryParse(parts[1], out var l))
            {
                limit = l;
            }
            else if (parts[0] == "offset" && int.TryParse(parts[1], out var o))
            {
                offset = o;
            }
        }

        return (limit, offset);
    }

    private sealed class TestMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder(request));
    }
}
