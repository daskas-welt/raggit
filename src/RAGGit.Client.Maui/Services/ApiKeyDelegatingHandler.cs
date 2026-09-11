using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Maui.Services;

public sealed class ApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly string _apiKey;
    private readonly string _headerName;

    public ApiKeyDelegatingHandler(string apiKey, string headerName = "X-Api-Key")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _headerName = headerName;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(_headerName) && !string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.TryAddWithoutValidation(_headerName, _apiKey);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
