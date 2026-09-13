using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T034: LoginViewModel drives HTTPS login, surfaces actionable cert-trust errors,
/// and never bypasses certificate validation.
/// </summary>
public sealed class LoginViewModelTests
{
    [Fact]
    public async Task LoginAsync_Success_UpdatesSessionAndInvokesCallback()
    {
        var callbackInvoked = false;
        var session = new ClientSession { WorkstationUrl = "https://workstation.local" };
        var handler = new TestMessageHandler(request =>
        {
            if (request.RequestUri!.PathAndQuery == "/api/auth/login")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(
                            new
                            {
                                access_token = "token-a",
                                token_type = "Bearer",
                                expires_in = 28800,
                            }
                        )
                    ),
                };
            }

            if (request.RequestUri.PathAndQuery == "/api/auth/me")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(
                            new
                            {
                                identityType = "Local",
                                role = "Admin",
                                displayName = "Ada Lovelace",
                                username = "ada",
                                sub = "a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11",
                            }
                        )
                    ),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") };
        var auth = new AuthApiClient(client);
        var vm = new LoginViewModel(
            auth,
            session,
            onLoginSuccess: () =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }
        );

        vm.Username = "ada";
        vm.Password = "valid-pass-1";
        await vm.LoginCommand.ExecuteAsync(null);

        callbackInvoked.Should().BeTrue();
        session.Role.Should().Be("Admin");
        session.IdentityType.Should().Be("Local");
        vm.ErrorMessage.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_Unauthorized_ShowsGenericFailure()
    {
        var session = new ClientSession { WorkstationUrl = "https://workstation.local" };
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Unauthorized
        ));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://workstation.local/") };
        var auth = new AuthApiClient(client);
        var vm = new LoginViewModel(auth, session);

        vm.Username = "bob";
        vm.Password = "wrong";
        await vm.LoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("username");
    }

    [Fact]
    public async Task LoginAsync_CertificateError_ShowsActionableTrustError()
    {
        var session = new ClientSession { WorkstationUrl = "https://untrusted.local" };
        var handler = new TestMessageHandler(_ =>
            throw new HttpRequestException(
                "The SSL connection could not be established: Remote certificate is invalid according to the validation procedure."
            )
        );
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://untrusted.local/") };
        var auth = new AuthApiClient(client);
        var vm = new LoginViewModel(auth, session);

        vm.Username = "bob";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("certificate");
        vm.ErrorMessage.Should().Contain("trust");
    }

    [Fact]
    public async Task LoginAsync_NonHttpsWorkstationUrl_ShowsHttpsRequiredError()
    {
        var session = new ClientSession { WorkstationUrl = "http://workstation.local" };
        var handler = new TestMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://workstation.local/") };
        var auth = new AuthApiClient(client);
        var vm = new LoginViewModel(auth, session);

        vm.Username = "bob";
        vm.Password = "pass";
        await vm.LoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.ErrorMessage.Should().Contain("HTTPS");
    }

    private sealed class TestMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_handler(request));
    }
}
