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
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// POST /api/queries
    /// </summary>
    public async Task<QueryResponse> QueryAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var request = new QueryRequest { Query = query, TopK = topK };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/queries",
                request,
                _jsonOptions,
                cancellationToken
            );

            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<QueryResponse>(
                _jsonOptions,
                cancellationToken
            );
            return result ?? new QueryResponse();
        }
        catch (HttpRequestException ex) when (IsMappedError(ex))
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync(ct);
        if (
            response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
            && body.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        )
            throw new HttpRequestException("model unavailable offline");
        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException($"AI workstation unavailable: {body}");
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new HttpRequestException($"unauthorized: {body}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new HttpRequestException($"forbidden: {body}");
        response.EnsureSuccessStatusCode();
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
}
