using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Attaches the cached Bearer token to outgoing requests and clears the cache when
/// the workstation responds with 401. Used for per-person actions; the separate
/// <see cref="ApiKeyDelegatingHandler"/> remains for bootstrap-only calls.
/// </summary>
public sealed class BearerDelegatingHandler : DelegatingHandler
{
    private readonly ISessionTokenStore _store;

    public BearerDelegatingHandler(ISessionTokenStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (!request.Headers.Contains("Authorization"))
        {
            var session = await _store.GetAsync(cancellationToken);
            if (session is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    session.Token
                );
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _store.ClearAsync(cancellationToken);
        }

        return response;
    }
}
