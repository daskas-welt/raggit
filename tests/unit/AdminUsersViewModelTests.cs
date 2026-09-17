using System;
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
/// 006-client-architecture US1: AdminUsersViewModel is exercised with no UI
/// shell; forbidden admin operations surface an error rather than throwing.
/// </summary>
public sealed class AdminUsersViewModelTests
{
    [Fact]
    public async Task LoadUsers_Populates()
    {
        var vm = CreateViewModel(_ => UsersResponse());

        await vm.LoadUsersCommand.ExecuteAsync(null);

        vm.Users.Should().HaveCount(2);
        vm.Users[0].Username.Should().Be("ada");
        vm.ErrorMessage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUser_Forbidden_SetsErrorMessage()
    {
        var vm = CreateViewModel(request =>
            request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("forbidden"),
                }
                : UsersResponse()
        );
        vm.NewUsername = "new";
        vm.NewDisplayName = "New Person";
        vm.NewPassword = "P@ssw0rd!";

        await vm.CreateUserCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("forbidden");
    }

    [Fact]
    public async Task LoadUsers_Forbidden_SetsErrorMessageAndNoData()
    {
        var vm = CreateViewModel(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("forbidden"),
        });

        await vm.LoadUsersCommand.ExecuteAsync(null);

        vm.Users.Should().BeEmpty();
        vm.ErrorMessage.Should().Contain("forbidden");
    }

    private static AdminUsersViewModel CreateViewModel(
        Func<HttpRequestMessage, HttpResponseMessage> responder
    )
    {
        var api = new UsersApiClient(
            new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://w.local/") }
        );
        return new AdminUsersViewModel(api);
    }

    private static HttpResponseMessage UsersResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new object[]
                    {
                        new
                        {
                            id = Guid.NewGuid(),
                            username = "ada",
                            displayName = "Ada",
                            role = "Admin",
                            isActive = true,
                            mustChangePassword = false,
                            lockedOut = false,
                            createdAt = DateTime.UtcNow,
                        },
                        new
                        {
                            id = Guid.NewGuid(),
                            username = "bob",
                            displayName = "Bob",
                            role = "Employee",
                            isActive = true,
                            mustChangePassword = false,
                            lockedOut = false,
                            createdAt = DateTime.UtcNow,
                        },
                    }
                )
            ),
        };

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
