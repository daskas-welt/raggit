using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Thin client for GET /api/queries/history + GET /api/queries/{id} (005-per-person-history).
/// Registered with <see cref="BearerDelegatingHandler"/> so the person JWT
/// from <see cref="ISessionTokenStore"/> scopes the read; never sends keys.
/// </summary>
public sealed class QueryHistoryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public QueryHistoryApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// GET api/queries/history?limit=&amp;offset= — nulls omitted, server clamps.
    /// </summary>
    public async Task<HistoryPage> GetHistoryAsync(
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = "api/queries/history";
        var separator = "?";
        if (limit is not null)
        {
            query += $"{separator}limit={limit.Value}";
            separator = "&";
        }

        if (offset is not null)
        {
            query += $"{separator}offset={offset.Value}";
        }

        try
        {
            var response = await _httpClient.GetAsync(query, cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var page = await response.Content.ReadFromJsonAsync<HistoryPage>(
                _jsonOptions,
                cancellationToken
            );
            return page ?? new HistoryPage();
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

    /// <summary>
    /// GET api/queries/{id} — full prompt/answer + citations for one owned query.
    /// Throws on 401 (unauthorized) / 404 (not found or not owned).
    /// </summary>
    public async Task<QueryDetail> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/queries/{id:D}", cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var detail = await response.Content.ReadFromJsonAsync<QueryDetail>(
                _jsonOptions,
                cancellationToken
            );
            return detail ?? new QueryDetail { Id = id };
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
        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException($"AI workstation unavailable: {body}");
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new HttpRequestException($"unauthorized: {body}");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new HttpRequestException($"forbidden: {body}");
        response.EnsureSuccessStatusCode();
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);
}
