using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Core.Services;

/// <summary>A generated document returned by the workstation.</summary>
public sealed class DocumentFile
{
    public DocumentFile(byte[] bytes, string fileName, string contentType)
    {
        Bytes = bytes;
        FileName = fileName;
        ContentType = contentType;
    }

    public byte[] Bytes { get; }
    public string FileName { get; }
    public string ContentType { get; }
}

/// <summary>
/// Thin client for <c>POST /api/queries/{id}/documents</c> (033). The response
/// is a binary file, so errors are mapped to the same user vocabulary as the
/// query client rather than parsed as JSON answers.
/// </summary>
public sealed class DocumentApiClient
{
    private readonly HttpClient _httpClient;

    public DocumentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DocumentFile> GetAsync(
        Guid queryId,
        DocumentOutputFormat format,
        CancellationToken cancellationToken = default
    )
    {
        if (queryId == Guid.Empty)
        {
            throw new ArgumentException("A persisted query id is required.", nameof(queryId));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/queries/{queryId}/documents"
        )
        {
            Content = JsonContent.Create(
                new DocumentRequest { Format = format.WireValue() },
                options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
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
            System.Net.HttpStatusCode.BadRequest => new HttpRequestException(
                $"unsupported format: {message}"
            ),
            System.Net.HttpStatusCode.Unauthorized => new HttpRequestException(
                "unauthorized: please sign in again"
            ),
            System.Net.HttpStatusCode.Forbidden => new HttpRequestException(
                "forbidden: you do not have access to this answer"
            ),
            System.Net.HttpStatusCode.NotFound => new HttpRequestException(
                "This answer is no longer available."
            ),
            System.Net.HttpStatusCode.UnprocessableEntity => new HttpRequestException(message),
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
