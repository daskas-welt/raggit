using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 029-client-ux-optimization T005: the Library search filters the loaded list
/// as the text changes, pages over the filtered set, and re-applies after a
/// refresh (FR-001–FR-004, SC-002).
/// </summary>
public sealed class LibrarySearchTests
{
    [Fact]
    public async Task Search_MatchesFilename_CaseInsensitively()
    {
        var vm = await LoadedAsync(3);

        vm.SearchText = "DOC 1";

        vm.FilteredDocuments.Should().ContainSingle().Which.Filename.Should().Be("doc 1.pdf");
        vm.MatchCount.Should().Be(1);
        vm.IsSearchActive.Should().BeTrue();
    }

    [Fact]
    public async Task Search_Punctuation_MatchesLiterally()
    {
        var vm = await LoadedAsync(3);

        vm.SearchText = "doc 1.";

        vm.FilteredDocuments.Should().ContainSingle().Which.Filename.Should().Be("doc 1.pdf");
    }

    [Fact]
    public async Task Search_NoMatch_ReportsZero_WithoutError()
    {
        var vm = await LoadedAsync(3);

        vm.SearchText = "does-not-exist";

        vm.FilteredDocuments.Should().BeEmpty();
        vm.MatchCount.Should().Be(0);
        vm.HasMatch.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Search_EmptyText_ReturnsEverything()
    {
        var vm = await LoadedAsync(3);
        vm.SearchText = "doc 1";

        vm.SearchText = "   ";

        vm.IsSearchActive.Should().BeFalse();
        vm.MatchCount.Should().Be(3);
        vm.FilteredDocuments.Should().HaveCount(3);
    }

    [Fact]
    public async Task Search_PagesOverTheFilteredSet_AndResetsToFirstPage()
    {
        var vm = await LoadedAsync(60);
        vm.PageSize = 20;

        vm.SearchText = "doc";

        // Every filename contains "doc", so the filtered total is the full 60.
        vm.MatchCount.Should().Be(60);
        vm.TotalPages.Should().Be(3);
        vm.PageItems.Should().HaveCount(20);

        vm.GoToPageCommand.Execute(2);
        vm.PageNumber.Should().Be(2);

        // Narrowing the search returns to page 1 rather than stranding the user.
        vm.SearchText = "doc 5.pdf";
        vm.PageNumber.Should().Be(1);
        vm.PageItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Search_MatchCaption_StatesFilteredCountOfTheFullLibrary()
    {
        var vm = await LoadedAsync(60);

        vm.SearchText = "doc 5.pdf";

        vm.MatchCaption.Should().Be("1 of 60 documents");
    }

    [Fact]
    public async Task ClearSearch_RestoresThePage_WhileANewQueryReturnsToPageOne()
    {
        var vm = await LoadedAsync(60);
        vm.PageSize = 20;
        vm.GoToPageCommand.Execute(3);
        vm.PageNumber.Should().Be(3);

        vm.SearchText = "doc";
        vm.PageNumber.Should().Be(1);

        vm.SearchText = "doc 5.pdf";
        vm.PageNumber.Should().Be(1);

        vm.SearchText = string.Empty;

        vm.PageNumber.Should().Be(3);
        vm.FilteredDocuments.Should().HaveCount(60);
    }

    [Fact]
    public async Task Search_StatusText_ReflectsTheFilteredTotal()
    {
        var vm = await LoadedAsync(60);
        vm.PageSize = 20;

        vm.SearchText = "doc 1";

        vm.StatusText.Should().Contain($"of {vm.MatchCount}");
    }

    [Fact]
    public async Task Refresh_ReappliesTheCurrentSearch()
    {
        var vm = await LoadedAsync(3);
        vm.SearchText = "doc 2";

        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.SearchText.Should().Be("doc 2");
        vm.FilteredDocuments.Should().ContainSingle().Which.Filename.Should().Be("doc 2.pdf");
    }

    [Fact]
    public void Search_SeedsFromSessionState_AndWritesBack()
    {
        var session = new SearchSessionState();
        session.Set(SearchSurface.Library, "persisted");
        var vm = CreateViewModel(_ => Response(0), session);

        vm.SearchText.Should().Be("persisted");

        vm.SearchText = "changed";

        session.Get(SearchSurface.Library).Should().Be("changed");
    }

    private static async Task<LibraryViewModel> LoadedAsync(int count)
    {
        var vm = CreateViewModel(_ => Response(count), new SearchSessionState());
        await vm.LoadDocumentsCommand.ExecuteAsync(null);
        return vm;
    }

    private static LibraryViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        SearchSessionState session
    )
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            new InMemoryLauncherService(),
            searchSession: session
        );
    }

    private static HttpResponseMessage Response(int count)
    {
        var documents = new object[count];
        for (var i = 0; i < count; i++)
        {
            documents[i] = new
            {
                id = Guid.NewGuid(),
                filename = $"doc {i}.pdf",
                mime = "application/pdf",
                size = 10,
                hash = "h",
                status = "Ready",
                createdBy = "sub",
                createdAt = DateTime.UtcNow,
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(documents)),
        };
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
