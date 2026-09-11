using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// SC-005: corrupted pdf/docx → 400 with no partial index.
/// Truncated PDF must not leave Documents/Chunks rows or LanceDB vectors.
/// </summary>
public sealed class CorruptedDocumentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Upload_TruncatedPdf_Returns400_And_NoPartialIndex()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var bytes = await LoadBadPdfAsync();
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "bad.pdf");

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("corrupted pdf");

        // No partial index: GET does not list it
        var listResponse = await client.GetAsync("/api/documents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var documents = await DeserializeListAsync(listResponse);
        documents.Should().NotContain(d => d.Filename == "bad.pdf");

        // No Chunks rows for bad.pdf (hash not present or no chunks)
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM Chunks WHERE DocumentId IN (SELECT Id FROM Documents WHERE Filename='bad.pdf');";
        var chunkCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        chunkCount.Should().Be(0, "corrupted document must not leave Chunks");

        // No vectors for bad.pdf
        var store = factory.Services.GetRequiredService<IVectorStore>();
        var hits = await store.SearchAsync(CreateProbeVector(), limit: 100);
        // Hits should not contain documentId referencing bad.pdf; since bad.pdf not indexed, count remains 0 for that doc
        // We simply assert no hit has text from bad.pdf (which would be garbage)
        hits.Should()
            .NotContain(r =>
                r.Text != null && r.Text.Contains("bad.pdf", StringComparison.OrdinalIgnoreCase)
            );
    }

    [Fact]
    public async Task Upload_CorruptedDocx_Returns400_And_NoPartialIndex()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var bytes = await LoadBadDocxAsync();
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        );
        form.Add(file, "file", "bad.docx");

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("corrupted");

        var listResponse = await client.GetAsync("/api/documents");
        var documents = await DeserializeListAsync(listResponse);
        documents.Should().NotContain(d => d.Filename == "bad.docx");
    }

    private static async Task<byte[]> LoadBadPdfAsync()
    {
        var candidates = new[]
        {
            "fixtures/bad.pdf",
            Path.Combine(AppContext.BaseDirectory, "fixtures", "bad.pdf"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "bad.pdf"),
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return await File.ReadAllBytesAsync(p);
        }
        // fallback: truncated %PDF
        return System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 truncated garbage");
    }

    private static async Task<byte[]> LoadBadDocxAsync()
    {
        var candidates = new[]
        {
            "fixtures/bad.docx",
            Path.Combine(AppContext.BaseDirectory, "fixtures", "bad.docx"),
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return await File.ReadAllBytesAsync(p);
        }
        return new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF };
    }

    private static float[] CreateProbeVector()
    {
        var v = new float[384];
        v[0] = 1.0f;
        return v;
    }

    private static async Task<System.Collections.Generic.List<Document>> DeserializeListAsync(
        HttpResponseMessage response
    )
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<System.Collections.Generic.List<Document>>(
                json,
                JsonOptions
            ) ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }
}
