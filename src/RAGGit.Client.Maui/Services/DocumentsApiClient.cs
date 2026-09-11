using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Client.Maui.Services;

/// <summary>
/// Thin client for the Workstation.Api document endpoints.
/// </summary>
public sealed class DocumentsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public DocumentsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// GET /api/documents
    /// </summary>
    public async Task<IReadOnlyList<Document>> GetDocumentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await _httpClient.GetAsync("api/documents", cancellationToken);
            await EnsureSuccessOrThrowAsync(response, cancellationToken);

            var documents = await response.Content.ReadFromJsonAsync<List<Document>>(
                _jsonOptions,
                cancellationToken
            );
            return documents ?? new List<Document>();
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
            response.StatusCode == HttpStatusCode.ServiceUnavailable
            && body.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        )
            throw new HttpRequestException("model unavailable offline");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new HttpRequestException($"AI workstation unavailable: {body}");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new HttpRequestException($"unauthorized: {body}");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new HttpRequestException($"forbidden: {body}");
        response.EnsureSuccessStatusCode();
    }

    private static bool IsMappedError(HttpRequestException ex) =>
        ex.Message.Contains("model unavailable offline", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("AI workstation unavailable", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("cannot reach AI workstation", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// POST /api/documents with a file stream and optional progress reporting.
    /// </summary>
    public async Task<Document> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var content = new MultipartFormDataContent();

        StreamContent fileContent;
        if (progress is not null && fileStream.CanSeek)
        {
            fileContent = new StreamContent(
                new ProgressStream(fileStream, progress, fileStream.Length)
            );
        }
        else
        {
            fileContent = new StreamContent(fileStream);
        }

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("api/documents", content, cancellationToken);
        }
        catch (HttpRequestException ex) when (!IsMappedError(ex))
        {
            throw new HttpRequestException($"cannot reach AI workstation: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new HttpRequestException("AI workstation unavailable: request timed out", ex);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(
                _jsonOptions,
                cancellationToken
            );
            throw new UnsupportedDocumentTypeException(
                error?.Error ?? $"unsupported type: {Path.GetExtension(fileName)}"
            );
        }

        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var document =
            await response.Content.ReadFromJsonAsync<Document>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize document response.");

        return document;
    }

    /// <summary>
    /// DELETE /api/documents/{id}
    /// </summary>
    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"api/documents/{documentId}",
                cancellationToken
            );
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
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

    private sealed class ErrorResponse
    {
        public string? Error { get; set; }
    }
}

/// <summary>
/// Thrown when the workstation rejects a file because of its type.
/// </summary>
public sealed class UnsupportedDocumentTypeException : Exception
{
    public UnsupportedDocumentTypeException(string message)
        : base(message) { }
}

/// <summary>
/// Wraps a stream and reports read progress as a percentage.
/// </summary>
internal sealed class ProgressStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<double> _progress;
    private readonly long _length;
    private long _read;

    public ProgressStream(Stream inner, IProgress<double> progress, long length)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _length = length > 0 ? length : 1;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        _read += read;
        _progress.Report(_read / (double)_length);
        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _read += read;
        _progress.Report(_read / (double)_length);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public override void Flush() => _inner.Flush();
}
