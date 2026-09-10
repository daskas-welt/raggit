using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest.Vector;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Integration tests for the ingestion pipeline: upload → chunk → embed → index.
/// Uses the real Workstation.Api with the LanceDB vector store at a temp path
/// and deterministic embedder/LLM fakes so the suite does not need Ollama.
/// Each test creates its own factory to keep test data isolated.
/// </summary>
public sealed class IngestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Upload_TenDocuments_BecomesReady_And_Searchable()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var sw = Stopwatch.StartNew();
        const int count = 10;

        for (var i = 0; i < count; i++)
        {
            var text = $"This is document number {i}. It contains enough text to produce at least one chunk for embedding. " +
                       $"The refund policy is described in document {i}.";
            var response = await UploadTextAsync(client, $"doc-{i}.txt", text);
            response.StatusCode.Should().Be(HttpStatusCode.Created, $"upload {i} should be accepted");
        }

        var listResponse = await client.GetAsync("/api/documents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var documents = await DeserializeListAsync(listResponse);
        documents.Should().HaveCount(count);
        documents.Should().OnlyContain(d => d.Status == DocumentStatus.Ready);

        var store = factory.Services.GetRequiredService<IVectorStore>();
        var hits = await store.SearchAsync(factory.QueryVector, limit: 100, cancellationToken: default);
        hits.Count.Should().BeGreaterThan(0, "LanceDB should contain indexed vectors");

        sw.Stop();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Upload_UnsupportedType_Returns400()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("MZ executable stub");
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "malware.exe");

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("unsupported type", "error should name the unsupported extension");
    }

    [Fact]
    public async Task Upload_OversizedFile_Returns413()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var form = new MultipartFormDataContent();
        var oversized = new LengthOnlyStream(DocumentValidation.MaxFileSizeBytes + 1);
        var file = new StreamContent(oversized);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "huge.pdf");

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Upload_DuplicateHash_Returns200ExistingDocument()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        const string content = "Duplicate integration test content.";
        var first = await UploadTextAsync(client, "duplicate.txt", content);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var existing = await DeserializeDocumentAsync(first);

        var second = await UploadTextAsync(client, "duplicate.txt", content);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicate = await DeserializeDocumentAsync(second);

        duplicate.Id.Should().Be(existing.Id);
        duplicate.Hash.Should().Be(existing.Hash);
    }

    private static async Task<HttpResponseMessage> UploadTextAsync(HttpClient client, string filename, string text)
    {
        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", filename);
        return await client.PostAsync("/api/documents", form);
    }

    private static async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document.");
    }

    private static async Task<List<Document>> DeserializeListAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Document>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }
}

/// <summary>
/// Factory for integration tests. Keeps SQLite/LanceDB in temp folders and
/// replaces Ollama with deterministic fakes.
/// </summary>
public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    public string AdminKey { get; } = "admin-integration-test";
    public string EmployeeKey { get; } = "employee-integration-test";
    public float[] QueryVector { get; } = CreateQueryVector();

    private readonly string _dbPath;
    private readonly string _lanceDbPath;

    public IntegrationTestFactory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "raggit-integration-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "rag.db");
        _lanceDbPath = Path.Combine(baseDir, "lancedb");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<ApiKeyAuthOptions>(ApiKeyAuthOptions.Scheme, options =>
            {
                options.AdminApiKey = AdminKey;
                options.EmployeeApiKey = EmployeeKey;
            });

            services.AddSingleton(new RagDbContext($"Data Source={_dbPath}"));
            services.AddSingleton<IVectorStore>(new LanceDbLocalClient(_lanceDbPath, 384));
            services.AddSingleton<IEmbedder>(new FakeEmbedder());
            services.AddSingleton<ILlmClient>(new FakeLlmClient());
        });
    }

    private static float[] CreateQueryVector()
    {
        var vector = new float[384];
        vector[0] = 1.0f;
        return vector;
    }
}

internal sealed class FakeEmbedder : IEmbedder
{
    public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var embeddings = inputs.Select(_ =>
        {
            var vector = new float[384];
            vector[0] = 1.0f;
            return vector;
        }).ToList();

        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }
}

internal sealed class FakeLlmClient : ILlmClient
{
    public string ResponseText { get; set; } = "integration answer";
    public bool Healthy { get; set; } = true;
    public bool ThrowOnChat { get; set; }

    public Task<string> ChatAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (ThrowOnChat)
        {
            throw new HttpRequestException("Simulated LLM unavailable offline.");
        }

        return Task.FromResult(ResponseText);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Healthy);
}

/// <summary>
/// A seekable stream that reports a fixed length and returns zeros. Used to
/// simulate a >100MB file without allocating memory.
/// </summary>
internal sealed class LengthOnlyStream : Stream
{
    private long _position;

    public LengthOnlyStream(long length)
    {
        Length = length;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position >= Length)
        {
            return 0;
        }

        var remaining = (int)Math.Min(count, Length - _position);
        Array.Clear(buffer, offset, remaining);
        _position += remaining;
        return remaining;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Flush() { }
}
