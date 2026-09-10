using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Thin client for the Workstation.Api query endpoint.
/// </summary>
public sealed class QueryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() }
    };

    public QueryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// POST /api/query
    /// </summary>
    public async Task<QueryResponse> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var request = new QueryRequest
        {
            Query = query,
            TopK = topK
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/query",
            request,
            _jsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<QueryResponse>(_jsonOptions, cancellationToken);
        return result ?? new QueryResponse();
    }
}
