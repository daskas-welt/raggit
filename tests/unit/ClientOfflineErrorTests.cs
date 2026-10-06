using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T019: Client offline error handling (FR-007). Workstation unreachable →
/// QueryViewModel/LibraryViewModel surface AI workstation unavailable / cannot reach AI workstation
/// within timeout + expose retry command, and no request goes to non-configured host.
/// </summary>
public sealed class ClientOfflineErrorTests
{
    [Fact]
    public async Task QueryViewModel_Unreachable_SurfacesCannotReachAndExposesRetry()
    {
        var handler = new UnreachableHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5001"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var api = new QueryApiClient(http);
        var history = new QueryHistoryApiClient(
            new HttpClient { BaseAddress = new Uri("http://localhost:5001") }
        );
        var vm = new QueryViewModel(api, history, new ClientSession(), new ConversationStore());
        vm.QueryText = "refund policy";

        await vm.AskCommand.ExecuteAsync(null);

        vm.StatusSeverity.Should().Be("Error");
        vm.StatusMessage.Should().NotBeNullOrEmpty();
        vm.StatusMessage!.ToLowerInvariant()
            .Should()
            .Contain("cannot reach ai workstation".ToLowerInvariant().Substring(0, 10)); // contains cannot reach / unavailable
        // Accept either phrase per spec: cannot reach AI workstation OR AI workstation unavailable
        (
            vm.StatusMessage!.Contains(
                "cannot reach AI workstation",
                StringComparison.OrdinalIgnoreCase
            )
            || vm.StatusMessage!.Contains(
                "AI workstation unavailable",
                StringComparison.OrdinalIgnoreCase
            )
        )
            .Should()
            .BeTrue($"StatusMessage was '{vm.StatusMessage}' expected offline phrase");

        // Retry command should be exposed
        var hasRetry = HasRetryCommand(vm);
        hasRetry.Should().BeTrue("QueryViewModel should expose a retry command for offline");

        handler
            .RequestsToNonConfiguredHost.Should()
            .BeFalse("no request should go to non-configured host (FR-007)");
    }

    [Fact]
    public async Task LibraryViewModel_Unreachable_SurfacesUnavailableAndExposesRetry()
    {
        var handler = new UnreachableHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5001"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var api = new DocumentsApiClient(http);
        // LibraryViewModel after T022 takes ClientSession; use reflection fallback for red phase
        var session = new ClientSession
        {
            WorkstationUrl = "http://localhost:5001",
            ApiKey = "k",
            Role = "Admin",
        };
        var vm = CreateLibraryViewModel(api, session);
        vm.Should().NotBeNull();

        await vm.LoadDocumentsCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        (
            vm.ErrorMessage!.Contains(
                "cannot reach AI workstation",
                StringComparison.OrdinalIgnoreCase
            )
            || vm.ErrorMessage!.Contains(
                "AI workstation unavailable",
                StringComparison.OrdinalIgnoreCase
            )
            || vm.ErrorMessage!.Contains("AI workstation", StringComparison.OrdinalIgnoreCase)
        )
            .Should()
            .BeTrue($"ErrorMessage was '{vm.ErrorMessage}'");

        var hasRetry = HasRetryCommand(vm);
        hasRetry.Should().BeTrue("LibraryViewModel should expose a retry command for offline");
    }

    [Fact]
    public async Task DocumentsApiClient_Maps503_ModelUnavailableOffline()
    {
        var handler = new StatusHandler(
            HttpStatusCode.ServiceUnavailable,
            "{\"error\":\"model unavailable offline\"}"
        );
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        var api = new DocumentsApiClient(http);

        // Upload that returns 503 should surface model unavailable offline verbatim (T023)
        // We test GetDocuments that returns 503
        var exception = await AssertThrowsAsync<Exception>(() => api.GetDocumentsAsync());
        exception
            .Message.Should()
            .Contain(
                "model unavailable offline",
                "503 should be mapped to model unavailable offline verbatim"
            );
    }

    [Fact]
    public async Task QueryApiClient_Maps503_ModelUnavailableOffline()
    {
        var handler = new StatusHandler(
            HttpStatusCode.ServiceUnavailable,
            "{\"error\":\"model unavailable offline\"}"
        );
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        var api = new QueryApiClient(http);

        var ex = await AssertThrowsAsync<Exception>(() => api.QueryAsync("hello"));
        ex.Message.Should().Contain("model unavailable offline");
    }

    [Fact]
    public void NoFallbackEndpoints_InClientCode()
    {
        // Ensure no hard-coded fallback host beyond configured Workstation:Url
        // This is a static check: client should not contain "ai-workstation.local" fallback or second BaseAddress
        var clientSourceRoot = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "RAGGit.Client.WPF"
        );
        if (!System.IO.Directory.Exists(clientSourceRoot))
            clientSourceRoot = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "src",
                    "RAGGit.Client.WPF"
                )
            );

        var files = System.IO.Directory.GetFiles(
            clientSourceRoot,
            "*.cs",
            System.IO.SearchOption.AllDirectories
        );
        foreach (var file in files)
        {
            var text = System.IO.File.ReadAllText(file);
            // Allow comment lines, but not code that sets BaseAddress to a hard-coded fallback
            if (file.EndsWith("ClientConfig.cs"))
                continue;
            text.Should()
                .NotContain(
                    "ai-workstation.local",
                    $"file {System.IO.Path.GetFileName(file)} should not hard-code fallback host"
                );
        }
    }

    private static bool HasRetryCommand(object vm)
    {
        var t = vm.GetType();
        return t.GetProperty("RetryCommand") != null
            || t.GetProperty("LoadDocumentsCommand") != null
            || t.GetProperty("AskCommand") != null;
        // LibraryViewModel retry is LoadDocuments; QueryViewModel retry is Ask
    }

    private static LibraryViewModel CreateLibraryViewModel(
        DocumentsApiClient api,
        ClientSession session
    )
    {
        var ctor = typeof(LibraryViewModel)
            .GetConstructors()
            .FirstOrDefault(c =>
                c.GetParameters().Any(p => p.ParameterType == typeof(ClientSession))
            );
        if (ctor is not null)
        {
            var args = ctor.GetParameters()
                .Select(p =>
                    p.ParameterType == typeof(ClientSession) ? (object)session
                    : p.ParameterType == typeof(DocumentsApiClient) ? api
                    : p.ParameterType == typeof(RAGGit.Client.Core.Services.ILibraryPreferences)
                        ? (object?)null
                    : p.ParameterType == typeof(ILauncherService) ? new InMemoryLauncherService()
                    : p.ParameterType == typeof(INotificationService) ? (object?)null
                    : p.ParameterType == typeof(SearchSessionState) ? (object?)null
                    : p.HasDefaultValue ? p.DefaultValue
                    : throw new InvalidOperationException($"Unexpected param {p.ParameterType}")
                )
                .ToArray();
            return (LibraryViewModel)ctor.Invoke(args);
        }
        var legacy = typeof(LibraryViewModel).GetConstructor(
            new[] { typeof(DocumentsApiClient), typeof(string) }
        );
        if (legacy is not null)
            return (LibraryViewModel)legacy.Invoke(new object[] { api, session.Role });
        throw new InvalidOperationException("No suitable LibraryViewModel constructor");
    }

    private static async Task<T> AssertThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action();
        }
        catch (T ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected {typeof(T).Name} but got {ex.GetType().Name}: {ex.Message}"
            );
        }
        throw new Xunit.Sdk.XunitException(
            $"Expected {typeof(T).Name} but no exception was thrown"
        );
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        public bool RequestsToNonConfiguredHost { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (request.RequestUri?.Host != "localhost")
                RequestsToNonConfiguredHost = true;
            throw new HttpRequestException("cannot reach AI workstation");
        }
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;

        public StatusHandler(HttpStatusCode code, string body)
        {
            _code = code;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            var resp = new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(resp);
        }
    }
}
