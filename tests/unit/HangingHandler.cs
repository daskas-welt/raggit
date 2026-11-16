using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Holds an HTTP request open until the caller cancels — the shape of a broad
/// answer on a slow workstation, and of the stream a stopped question aborts.
/// </summary>
internal sealed class HangingHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }
}
