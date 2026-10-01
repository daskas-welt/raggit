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
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US1: LibraryViewModel is exercised with no UI shell.
/// Load populates the list; delete surfaces a forbidden error; role refresh
/// follows the session.
/// </summary>
public sealed class LibraryViewModelTests
{
    [Fact]
    public async Task Load_PopulatesDocuments()
    {
        var vm = CreateViewModel(_ => DocumentsResponse(2));

        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.Documents.Should().HaveCount(2);
        vm.Documents[0].Filename.Should().Be("doc 0.pdf");
        vm.ErrorMessage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Forbidden_SetsErrorMessage()
    {
        var vm = CreateViewModel(request =>
            request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("forbidden"),
                }
                : DocumentsResponse(1)
        );

        await vm.DeleteDocumentCommand.ExecuteAsync(
            new Document { Id = Guid.NewGuid(), Filename = "x.pdf" }
        );

        vm.ErrorMessage.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task RowAction_DeletesThenReloads()
    {
        var deletes = 0;
        var gets = 0;
        var vm = CreateViewModel(request =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                deletes++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            gets++;
            return DocumentsResponse(1);
        });

        await vm.RowActionCommand.ExecuteAsync(
            new Document { Id = Guid.NewGuid(), Filename = "x.pdf" }
        );

        deletes.Should().Be(1);
        gets.Should().Be(1);
    }

    [Fact]
    public async Task RowAction_FailedDelete_PreservesError()
    {
        var vm = CreateViewModel(request =>
            request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("forbidden"),
                }
                : DocumentsResponse(1)
        );

        await vm.RowActionCommand.ExecuteAsync(
            new Document { Id = Guid.NewGuid(), Filename = "x.pdf" }
        );

        // The follow-up reload must not wipe the delete failure.
        vm.ErrorMessage.Should().Contain("Forbidden");
    }

    [Fact]
    public void RefreshRole_FollowsSession()
    {
        var session = new ClientSession { Role = "Employee" };
        var vm = CreateViewModel(_ => DocumentsResponse(0), session);
        vm.IsAdmin.Should().BeFalse();

        session.Role = "Admin";
        vm.RefreshRole();

        vm.IsAdmin.Should().BeTrue();
    }

    private static LibraryViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        ClientSession? session = null
    )
    {
        var api = new DocumentsApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new LibraryViewModel(
            api,
            session ?? new ClientSession { WorkstationUrl = "https://w.local", ApiKey = "k" },
            new InMemoryLauncherService()
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
