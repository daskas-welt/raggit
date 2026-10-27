using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Thin client for the Workstation.Api query endpoints.
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
    /// POST /api/queries for a grounded answer with citations.
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

    /// <summary>
    /// POST /api/queries/stream — the same question, narrated as it is answered
    /// (032). Yields <c>progress</c> updates in the order the workstation
    /// performs the work, then exactly one terminal <c>answer</c> or
    /// <c>error</c> event. Unknown event types and malformed lines are skipped;
    /// a stream that ends without a terminal event is surfaced as a failure
    /// rather than an empty answer.
    /// </summary>
    /// <remarks>
    /// Deliberately does not use <c>ConfigureAwait(false)</c>: the ViewModel's
    /// continuations must stay on the caller's synchronization context (the WPF
    /// UI thread) so reasoning rows can be appended to a bound collection. This
    /// matches every other service in this assembly.
    /// </remarks>
    public async IAsyncEnumerable<QueryStreamEvent> StreamAsync(
        string query,
        int topK = 5,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var request = new QueryRequest { Query = query, TopK = topK };

        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "api/queries/stream")
            {
                Content = JsonContent.Create(request, options: _jsonOptions),
            };

            response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
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

        using (response)
        {
            // Validation/auth/unhealthy failures arrive as ordinary statuses
            // before the stream starts, and map exactly as they do today.
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var body = await ReadBodyAsync(response, cancellationToken);
            using var reader = new StreamReader(body, Encoding.UTF8);

            var sawTerminalEvent = false;
            while (true)
            {
                var line = await ReadLineAsync(reader, cancellationToken);
                if (line is null)
                {
                    break;
                }

                var streamEvent = ParseEvent(line);
                if (streamEvent is null)
                {
                    continue;
                }

                if (streamEvent.IsAnswer || streamEvent.IsError)
                {
                    sawTerminalEvent = true;
                }

                yield return streamEvent;
            }

            if (!sawTerminalEvent)
            {
                throw new HttpRequestException(
                    "AI workstation unavailable: response ended before the answer was received"
                );
            }
        }
    }

    private static async Task<Stream> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"AI workstation unavailable: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new HttpRequestException($"AI workstation unavailable: {ex.Message}", ex);
        }
    }

    private static async Task<string?> ReadLineAsync(
        StreamReader reader,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await reader.ReadLineAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException($"AI workstation unavailable: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new HttpRequestException($"AI workstation unavailable: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }
    }

    /// <summary>
    /// Parses one NDJSON line. Unknown event types and malformed lines are
    /// ignored (forward compatibility, contracts/api.yaml 1.6.0).
    /// </summary>
    private QueryStreamEvent? ParseEvent(string line)
    {
        var candidate = line.Trim();
        if (candidate.Length == 0)
        {
            return null;
        }

        try
        {
            var streamEvent = JsonSerializer.Deserialize<QueryStreamEvent>(candidate, _jsonOptions);
            if (streamEvent is null || string.IsNullOrWhiteSpace(streamEvent.Type))
            {
                return null;
            }

            if (!streamEvent.IsProgress && !streamEvent.IsAnswer && !streamEvent.IsError)
            {
                return null;
            }

            return streamEvent;
        }
        catch (JsonException)
        {
            return null;
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
