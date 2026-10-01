using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using RAGGit.Core.Models;
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

    [Fact]
    public async Task ChangeRoleTo_UsesRequestedRoleAndUpdatesUser()
    {
        var id = Guid.NewGuid();
        var requestedRole = UserRole.Admin;
        var api = new UsersApiClient(
            new HttpClient(
                new AsyncStubHandler(async request =>
                {
                    if (request.Method != HttpMethod.Patch)
                    {
                        return await Task.FromResult(UsersResponse());
                    }

                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                    requestedRole = Enum.Parse<UserRole>(
                        body.RootElement.GetProperty("role").GetString()!
                    );
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(
                                new
                                {
                                    id,
                                    username = "ada",
                                    displayName = "Ada",
                                    role = "Admin",
                                    isActive = true,
                                    mustChangePassword = false,
                                    lockedOut = false,
                                    createdAt = DateTime.UtcNow,
                                }
                            )
                        ),
                    };
                })
            )
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var vm = new AdminUsersViewModel(api);
        var user = new UserAccountDto
        {
            Id = id,
            Username = "ada",
            DisplayName = "Ada",
            Role = UserRole.Employee,
            IsActive = true,
        };
        vm.Users.Add(user);

        await vm.ChangeRoleToCommand.ExecuteAsync(new UserRoleChangeRequest(user, UserRole.Admin));

        requestedRole.Should().Be(UserRole.Admin);
        vm.Users.Should().ContainSingle().Which.Role.Should().Be(UserRole.Admin);
        vm.ErrorMessage.Should().BeNull();
        vm.StatusSeverity.Should().Be("Success");
    }

    [Fact]
    public async Task ChangeRoleTo_ConflictShowsServerMessageAndPreservesRole()
    {
        const string message =
            "The last active Admin cannot be demoted or deactivated. Promote another Admin first.";
        var user = new UserAccountDto
        {
            Id = Guid.NewGuid(),
            Username = "sole-admin",
            DisplayName = "Sole Admin",
            Role = UserRole.Admin,
            IsActive = true,
        };
        var vm = CreateViewModel(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = message }),
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        });
        vm.Users.Add(user);

        await vm.ChangeRoleToCommand.ExecuteAsync(
            new UserRoleChangeRequest(user, UserRole.Employee)
        );

        vm.ErrorMessage.Should().Be(message);
        vm.StatusMessage.Should().Be(message);
        vm.Users.Should().ContainSingle().Which.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task ChangeRoleTo_DoesNotStartAnotherUpdateWhileBusy()
    {
        var id = Guid.NewGuid();
        var user = new UserAccountDto
        {
            Id = id,
            Username = "ada",
            DisplayName = "Ada",
            Role = UserRole.Employee,
            IsActive = true,
        };
        var pendingResponse = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var firstRequestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var requestCount = 0;
        var api = new UsersApiClient(
            new HttpClient(
                new AsyncStubHandler(async _ =>
                {
                    Interlocked.Increment(ref requestCount);
                    firstRequestStarted.TrySetResult();
                    return await pendingResponse.Task;
                })
            )
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var vm = new AdminUsersViewModel(api);
        vm.Users.Add(user);

        var firstUpdate = vm.ChangeRoleToCommand.ExecuteAsync(
            new UserRoleChangeRequest(user, UserRole.Admin)
        );
        await firstRequestStarted.Task;
        vm.IsBusy.Should().BeTrue();
        await vm.ChangeRoleToCommand.ExecuteAsync(new UserRoleChangeRequest(user, UserRole.Admin));
        var requestsWhileBusy = Volatile.Read(ref requestCount);

        pendingResponse.SetResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            id,
                            username = "ada",
                            displayName = "Ada",
                            role = "Admin",
                            isActive = true,
                            mustChangePassword = false,
                            lockedOut = false,
                            createdAt = DateTime.UtcNow,
                        }
                    )
                ),
            }
        );
        await firstUpdate;

        requestsWhileBusy.Should().Be(1);
    }

    [Fact]
    public async Task ResetPassword_SuccessIsRetainedWhenDirectoryRefreshFails()
    {
        var user = new UserAccountDto
        {
            Id = Guid.NewGuid(),
            Username = "ada",
            DisplayName = "Ada",
            Role = UserRole.Employee,
            IsActive = true,
        };
        var api = new UsersApiClient(
            new HttpClient(
                new AsyncStubHandler(request =>
                    Task.FromResult(
                        request.RequestUri!.AbsolutePath.EndsWith("/reset-password")
                            ? new HttpResponseMessage(HttpStatusCode.NoContent)
                            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                            {
                                Content = new StringContent("directory unavailable"),
                            }
                    )
                )
            )
            {
                BaseAddress = new Uri("https://w.local/"),
            }
        );
        var vm = new AdminUsersViewModel(api)
        {
            ResetPassword = "GoodPassword123!",
            ResetMustChangePassword = true,
        };

        await vm.ResetPasswordCommand.ExecuteAsync(user);

        vm.PasswordResetCompleted.Should().BeTrue();
        vm.StatusSeverity.Should().Be("Warning");
        vm.StatusMessage.Should().Contain("Password reset for 'ada'");
        vm.StatusMessage.Should().Contain("directory");
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

    private sealed class AsyncStubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => responder(request);
    }
}
