using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Contract tests for <c>GET /api/documents/{id}/content</c> per
/// contracts/api.yaml (009, 1.5.0 delta). Runs against the full
/// Workstation.Api via <see cref="WebApplicationFactory{Program}"/>.
/// </summary>
public sealed class DocumentsContentContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public DocumentsContentContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
    }

    [Fact]
    public async Task Get_Content_WithoutCredential_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/documents/{Guid.NewGuid()}/content");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Content_Returns200_ByteRoundTrip()
    {
        var pdfBytes = BuildMinimalPdf("Content round-trip contract content");
        var uploaded = await _client.PostAsync(
            "/api/documents",
            CreatePdfContentFromBytes(pdfBytes)
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await ReadDocumentIdAsync(uploaded);

        var response = await _client.GetAsync($"/api/documents/{documentId}/content");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var returned = await response.Content.ReadAsByteArrayAsync();
        returned.Should().Equal(pdfBytes);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition?.FileName.Should().Contain("contract-test.pdf");
    }

    [Fact]
    public async Task Get_Content_LegacyRowWithoutOriginal_Returns404()
    {
        var legacyId = await InsertLegacyRowAsync();

        var response = await _client.GetAsync($"/api/documents/{legacyId}/content");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("original unavailable");
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

    private async Task<string> ReadDocumentIdAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, _jsonOptions)?.Id.ToString()
            ?? throw new InvalidOperationException("Failed to deserialize Document response.");
    }

    private async Task<Guid> InsertLegacyRowAsync()
    {
        // Simulates a pre-feature row: explicit column list WITHOUT CreatedByName,
        // and no stored original bytes anywhere.
        var id = Guid.NewGuid();
        var db = _factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            @"INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
              VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt);";
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@filename", "legacy-contract.pdf");
        command.Parameters.AddWithValue("@mime", "application/pdf");
        command.Parameters.AddWithValue("@size", 128);
        command.Parameters.AddWithValue("@hash", $"legacy-contract-{id:N}");
        command.Parameters.AddWithValue("@status", "Ready");
        command.Parameters.AddWithValue("@createdBy", "admin");
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
        return id;
    }
}
