using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RAGGit.Core.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Contract tests for <c>POST /api/documents</c> and <c>GET /api/documents</c>
/// per contracts/api.yaml. Runs against the full Workstation.Api via
/// <see cref="WebApplicationFactory{Program}"/> with deterministic fakes for
/// the vector store and embedder so the tests do not require Ollama/LanceDB.
/// </summary>
public sealed class DocumentsContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public DocumentsContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
    }

    [Fact]
    public async Task Post_ValidPdf_Returns201Document()
    {
        var content = CreatePdfContent("Contract test PDF content");

        var response = await _client.PostAsync("/api/documents", content);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await DeserializeDocumentAsync(response);
        doc.Filename.Should().Be("contract-test.pdf");
        doc.Mime.Should().Be(DocumentMimeType.Pdf);
        doc.Status.Should().Be(DocumentStatus.Ready);
        doc.Size.Should().BeGreaterThan(0);
        doc.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Post_DuplicateHash_Returns200ExistingDocument()
    {
        var pdfBytes = BuildMinimalPdf("Duplicate hash contract content");

        var first = await _client.PostAsync("/api/documents", CreatePdfContentFromBytes(pdfBytes));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var existing = await DeserializeDocumentAsync(first);

        // Re-use the exact same PDF bytes so the SHA-256 hash matches.
        var second = await _client.PostAsync("/api/documents", CreatePdfContentFromBytes(pdfBytes));

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicate = await DeserializeDocumentAsync(second);
        duplicate.Id.Should().Be(existing.Id);
        duplicate.Hash.Should().Be(existing.Hash);
    }

    [Fact]
    public async Task Get_Documents_Returns200Array()
    {
        await _client.PostAsync("/api/documents", CreatePdfContent("List contract content"));

        var response = await _client.GetAsync("/api/documents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var array = await JsonSerializer.DeserializeAsync<List<Document>>(
            await response.Content.ReadAsStreamAsync(),
            _jsonOptions
        );
        array.Should().NotBeNull();
        array
            .Should()
            .Contain(d =>
                d.Filename == "contract-test.pdf" || d.Filename == "list-contract-content.pdf"
            );
    }

    private static MultipartFormDataContent CreatePdfContent(string text)
    {
        var bytes = BuildMinimalPdf(text);
        return CreatePdfContentFromBytes(bytes);
    }

    private static MultipartFormDataContent CreatePdfContentFromBytes(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "contract-test.pdf");
        return form;
    }

    private static byte[] BuildMinimalPdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        page.AddText(text, 12, new PdfPoint(50, 700), font);
        return builder.Build();
    }

    [Fact]
    public async Task Post_CorruptedPdf_Returns400CorruptedPdf()
    {
        var bytes = await LoadFixtureBytesAsync("bad.pdf");

        var stream = new MemoryStream(bytes);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "bad.pdf");

        var response = await _client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("corrupted pdf");
    }

    [Fact]
    public async Task Post_CorruptedDocx_Returns400Corrupted()
    {
        var bytes = await LoadFixtureBytesAsync("bad.docx");

        var stream = new MemoryStream(bytes);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        );
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "bad.docx");

        var response = await _client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("corrupted");
    }

    private static async Task<byte[]> LoadFixtureBytesAsync(string filename)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "fixtures", filename),
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "integration",
                "fixtures",
                filename
            ),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", filename),
            Path.Combine("tests", "integration", "fixtures", filename),
            Path.Combine("..", "integration", "fixtures", filename),
            $"fixtures/{filename}",
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p))
                return await File.ReadAllBytesAsync(p);
        }

        // Fallback: search up from base
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "tests",
                "integration",
                "fixtures",
                filename
            );
            if (File.Exists(candidate))
                return await File.ReadAllBytesAsync(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Fixture {filename} not found");
    }

    private async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document response.");
    }
}
