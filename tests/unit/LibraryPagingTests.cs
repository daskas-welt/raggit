using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 007-library-pagination US1 (red-first): the library pages client-side with a
/// 25-row default, status text, first/previous/numbered/next/last navigation,
/// clamping, and an empty zero-state — all without extra HTTP.
/// </summary>
public sealed class LibraryPagingTests
{
    [Fact]
    public async Task FirstPage_Shows25AndStatus()
    {
        var vm = CreateViewModel(30);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.PageSize.Should().Be(25);
        vm.PageNumber.Should().Be(1);
        vm.TotalCount.Should().Be(30);
        vm.TotalPages.Should().Be(2);
        vm.PageItems.Should().HaveCount(25);
        vm.StatusText.Should().Be("Showing 1–25 of 30 entries");
        vm.HasPrevious.Should().BeFalse();
        vm.HasNext.Should().BeTrue();
    }

    [Fact]
    public async Task NextPage_AdvancesRange()
    {
        var vm = CreateViewModel(30);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.NextPageCommand.Execute(null);

        vm.PageNumber.Should().Be(2);
        vm.PageItems.Should().HaveCount(5);
        vm.StatusText.Should().Be("Showing 26–30 of 30 entries");
        vm.HasPrevious.Should().BeTrue();
        vm.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task LastThenFirst_NavigateBothEnds()
    {
        var vm = CreateViewModel(30);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.LastPageCommand.Execute(null);
        vm.PageNumber.Should().Be(2);

        vm.FirstPageCommand.Execute(null);
        vm.PageNumber.Should().Be(1);
        vm.StatusText.Should().Be("Showing 1–25 of 30 entries");
    }

    [Fact]
    public async Task GoToPage_JumpsDirectly()
    {
        var vm = CreateViewModel(55);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.GoToPageCommand.Execute(3);

        vm.PageNumber.Should().Be(3);
        vm.StatusText.Should().Be("Showing 51–55 of 55 entries");
    }

    [Fact]
    public async Task OutOfRangePage_ClampsToLast()
    {
        var vm = CreateViewModel(30);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.GoToPageCommand.Execute(99);

        vm.PageNumber.Should().Be(2);
        vm.PageItems.Should().HaveCount(5);
    }

    [Fact]
    public async Task EmptyLibrary_ShowsZeroState()
    {
        var vm = CreateViewModel(0);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.TotalCount.Should().Be(0);
        vm.TotalPages.Should().Be(1);
        vm.PageItems.Should().BeEmpty();
        vm.StatusText.Should().Be("Showing 0 of 0 entries");
        vm.HasPrevious.Should().BeFalse();
        vm.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_ShrinksToLastValidPage()
    {
        var count = 30;
        var vm = CreateViewModel(() => count);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);
        vm.LastPageCommand.Execute(null);
        vm.PageNumber.Should().Be(2);

        count = 10;
        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.PageNumber.Should().Be(1);
        vm.StatusText.Should().Be("Showing 1–10 of 10 entries");
    }

    [Fact]
    public async Task SizeChange_ResetsToFirstPageAndPersists()
    {
        var preferences = new InMemoryLibraryPreferences();
        var vm = CreateViewModel(55, preferences);
        await vm.LoadDocumentsCommand.ExecuteAsync(null);
        vm.GoToPageCommand.Execute(3);
        vm.PageNumber.Should().Be(3);

        vm.ChangePageSizeCommand.Execute(50);

        vm.PageSize.Should().Be(50);
        vm.PageNumber.Should().Be(1);
        vm.StatusText.Should().Be("Showing 1–50 of 55 entries");
        preferences.GetPageSize().Should().Be(50);
    }

    [Fact]
    public void FreshViewModel_RestoresStoredSize()
    {
        var preferences = new InMemoryLibraryPreferences();
        preferences.SetPageSize(100);

        var vm = CreateViewModel(10, preferences);

        vm.PageSize.Should().Be(100);
    }

    [Fact]
    public void CorruptStoredSize_FallsBackToDefault()
    {
        var preferences = new InMemoryLibraryPreferences();
        preferences.SetPageSize(77);

        var vm = CreateViewModel(10, preferences);

        vm.PageSize.Should().Be(25);
    }

    private static LibraryViewModel CreateViewModel(int total) => CreateViewModel(() => total);

    private static LibraryViewModel CreateViewModel(int total, ILibraryPreferences preferences) =>
        CreateViewModel(() => total, preferences);

    private static LibraryViewModel CreateViewModel(Func<int> total) =>
        CreateViewModel(total, new InMemoryLibraryPreferences());

    private static LibraryViewModel CreateViewModel(
        Func<int> total,
        ILibraryPreferences preferences
    )
    {
        var handler = new StubHandler(_ => DocumentsResponse(total()));
        var api = new DocumentsApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            new InMemoryLauncherService(),
            preferences
        );
    }

    private static HttpResponseMessage DocumentsResponse(int count)
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
