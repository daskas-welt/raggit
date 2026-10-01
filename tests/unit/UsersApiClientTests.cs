using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class UsersApiClientTests
{
    [Fact]
    public async Task UpdateUserAsync_ConflictSurfacesPlainServerError()
    {
        const string message =
            "The last active Admin cannot be demoted or deactivated. Promote another Admin first.";
        using var http = new HttpClient(
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    $$"""{"error":"{{message}}"}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            })
        )
        {
            BaseAddress = new Uri("https://workstation.invalid/"),
        };
        var client = new UsersApiClient(http);

        var act = () => client.UpdateUserAsync(Guid.NewGuid(), new UpdateUserRequest());

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage(message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
