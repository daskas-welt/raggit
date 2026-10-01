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
/// 016-admin-redesign: the Admin page surfaces every outcome (success,
/// failure with reason and next step) in a dedicated status surface.
/// The ViewModel exposes StatusMessage/StatusSeverity/HasStatus alongside
/// the legacy ErrorMessage, and preserves entered values on failure.
/// Red-first: these fail until the presentation state is implemented.
/// </summary>
public sealed class AdminUsersStatusTests
{
    [Fact]
    public void EmptyState_VisibleOnlyWhenIdleAndEmpty()
    {
        var vm = CreateViewModel(_ => UsersResponse());
        var notified = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName!);

        vm.ShowEmptyUsers.Should().BeTrue();

        vm.IsBusy = true;
        vm.ShowEmptyUsers.Should().BeFalse();
        notified.Should().Contain(nameof(AdminUsersViewModel.ShowEmptyUsers));

        vm.IsBusy = false;
        vm.Users.Add(
            new UserAccountDto
            {
                Id = Guid.NewGuid(),
                Username = "ada",
                DisplayName = "Ada",
            }
        );
        vm.ShowEmptyUsers.Should().BeFalse();
    }

    [Fact]
    public async Task LoadUsers_EmptyResult_ShowsEmptyStateWhenIdle()
    {
        var vm = CreateViewModel(_ => EmptyUsersResponse());
        vm.IsBusy = true;

        await vm.LoadUsersCommand.ExecuteAsync(null);

        vm.IsBusy.Should().BeFalse();
        vm.Users.Should().BeEmpty();
        vm.ShowEmptyUsers.Should().BeTrue();
    }

    [Fact]
    public async Task LoadUsers_Success_SetsLoadedStatus()
    {
        var vm = CreateViewModel(_ => UsersResponse());

        await vm.LoadUsersCommand.ExecuteAsync(null);

        vm.HasStatus.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Success");
        vm.StatusMessage.Should().Contain("2 users");
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_Success_SetsSuccessStatusAndClearsDraft()
    {
        var vm = CreateViewModel(request =>
            request.Method == HttpMethod.Post
                ? SingleUserResponse("newbie", "Newbie", "Employee", true, false)
                : UsersResponse()
        );
        vm.NewUsername = "newbie";
        vm.NewDisplayName = "Newbie";
        vm.NewPassword = "P@ssw0rd!";

        await vm.CreateUserCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        vm.HasStatus.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Success");
        vm.StatusMessage.Should().Contain("newbie");
        vm.NewUsername.Should().BeEmpty();
        vm.NewPassword.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateUser_Duplicate_SetsErrorStatusAndPreservesInput()
    {
        var vm = CreateViewModel(request =>
            request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = new StringContent("username already exists"),
                }
                : UsersResponse()
        );
        vm.NewUsername = "ada";
        vm.NewDisplayName = "Ada Clone";
        vm.NewPassword = "P@ssw0rd!";

        await vm.CreateUserCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        vm.HasStatus.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Error");
        vm.StatusMessage.Should().Be(vm.ErrorMessage);
        vm.NewUsername.Should().Be("ada");
        vm.NewDisplayName.Should().Be("Ada Clone");
        vm.NewPassword.Should().Be("P@ssw0rd!");
    }

    [Fact]
    public async Task CreateUser_MissingUsername_SetsWarningNamingTheField()
    {
        var vm = CreateViewModel(_ => UsersResponse());
        vm.NewUsername = string.Empty;
        vm.NewDisplayName = "No Name";
        vm.NewPassword = "P@ssw0rd!";

        await vm.CreateUserCommand.ExecuteAsync(null);

        vm.HasStatus.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Warning");
        vm.StatusMessage.Should().Contain("Username");
    }

    [Fact]
    public async Task ToggleActive_Success_SetsSuccessStatus()
    {
        var id = Guid.NewGuid();
        var vm = CreateViewModel(_ =>
            SingleUserResponse("bob", "Bob", "Employee", false, false, id)
        );
        var user = new UserAccountDto
        {
            Id = id,
            Username = "bob",
            DisplayName = "Bob",
            Role = Core.Models.UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await vm.ToggleActiveCommand.ExecuteAsync(user);

        vm.ErrorMessage.Should().BeNull();
        vm.StatusSeverity.Should().Be("Success");
        vm.StatusMessage.Should().Contain("bob");
    }

    [Fact]
    public async Task ResetPassword_NoSelection_SetsWarningStatus()
    {
        var vm = CreateViewModel(_ => UsersResponse());

        await vm.ResetPasswordCommand.ExecuteAsync(null);

        vm.HasStatus.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Warning");
        vm.StatusMessage.Should().Contain("Select a user");
    }

    [Fact]
    public async Task ResetPassword_Success_SetsSuccessStatusAndClearsPassword()
    {
        var id = Guid.NewGuid();
        var vm = CreateViewModel(request =>
            request.RequestUri!.AbsolutePath.Contains(
                "reset-password",
                StringComparison.OrdinalIgnoreCase
            )
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : UsersResponse()
        );
        var user = new UserAccountDto
        {
            Id = id,
            Username = "bob",
            DisplayName = "Bob",
            Role = Core.Models.UserRole.Employee,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        vm.ResetPassword = "N3w-P@ss!";

        await vm.ResetPasswordCommand.ExecuteAsync(user);

        vm.ErrorMessage.Should().BeNull();
        vm.StatusSeverity.Should().Be("Success");
        vm.StatusMessage.Should().Contain("bob");
        vm.ResetPassword.Should().BeEmpty();
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

    private static HttpResponseMessage EmptyUsersResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(Array.Empty<object>())),
        };

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

    private static HttpResponseMessage SingleUserResponse(
        string username,
        string displayName,
        string role,
        bool isActive,
        bool lockedOut,
        Guid? id = null
    ) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        id = id ?? Guid.NewGuid(),
                        username,
                        displayName,
                        role,
                        isActive,
                        mustChangePassword = false,
                        lockedOut,
                        createdAt = DateTime.UtcNow,
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
