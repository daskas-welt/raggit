using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Thin client for <c>POST /api/documents/generate</c> (034). This is a separate
/// typed client from the 033 answer exporter because document composition can
/// take as long as a model generation.
/// </summary>
public sealed class DocumentCreationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public DocumentCreationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DocumentFile> CreateAsync(
        string description,
        DocumentOutputFormat format,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/documents/generate")
        {
            Content = JsonContent.Create(
                new DocumentCreationRequest
                {
                    Description = description,
                    Format = format.WireValue(),
                },
                options: _jsonOptions
            ),
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );
        }
        catch (HttpRequestException exception)
        {
            throw new HttpRequestException(
                $"cannot reach AI workstation: {exception.Message}",
                exception
            );
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(
                "AI workstation unavailable: request timed out",
                exception
            );
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw await MapErrorAsync(response, cancellationToken);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var disposition = response.Content.Headers.ContentDisposition;
            var fileName =
                disposition?.FileNameStar?.ToString().Trim('"')
                ?? disposition?.FileName?.Trim('"')
                ?? $"document{format.Extension()}";
            var contentType =
                response.Content.Headers.ContentType?.ToString() ?? format.ContentType();

            return new DocumentFile(bytes, fileName, contentType);
        }
    }

    private static async Task<HttpRequestException> MapErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ExtractError(body);

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new HttpRequestException(message),
            HttpStatusCode.Unauthorized => new HttpRequestException(
                "unauthorized: please sign in again"
            ),
            HttpStatusCode.Forbidden => new HttpRequestException(
                "forbidden: you do not have access to document creation"
            ),
            HttpStatusCode.UnprocessableEntity => new HttpRequestException(message),
            HttpStatusCode.ServiceUnavailable => new HttpRequestException(
                message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
                || message.Contains("model unavailable", StringComparison.OrdinalIgnoreCase)
                    ? message
                    : $"AI workstation unavailable: {message}"
            ),
            (HttpStatusCode)499 => new HttpRequestException("request cancelled"),
            _ => new HttpRequestException($"AI workstation unavailable: {message}"),
        };
    }

    private static string ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "document generation failed";
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? "document generation failed"
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
