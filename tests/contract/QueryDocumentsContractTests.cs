using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using UglyToad.PdfPig;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// 033: contract for <c>POST /api/queries/{id}/documents</c> — ownership,
/// format/content type, refusal/citationless 422, invalid format 400, and the
/// absence of any model call.
/// </summary>
public sealed class QueryDocumentsContractTests : IClassFixture<TestApiFactory>
{
    private const string ChunkText = "Customers may return items within 30 days for a full refund.";

    private readonly TestApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public QueryDocumentsContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Render_Docx_Pdf_And_Txt_FromAnOwnedAnswer()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        await SeedChunksAsync(documentId, new[] { (chunkId, 0, ChunkText) });
        await SeedQueryAsync(
            queryId,
            sub,
            "Create a document about the refund policy.",
            $"Customers may return items within 30 days. [{chunkId}]",
            new[] { chunkId }
        );

        // The document path must not depend on the model being healthy.
        GetLlmClient().Healthy = false;
        var client = CreateBearerClient(token);

        var docx = await PostDocumentAsync(client, queryId, "docx");
        docx.StatusCode.Should().Be(HttpStatusCode.OK);
        docx.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        docx.Content.Headers.ContentDisposition!.FileName.Should().EndWith(".docx");
        using (var stream = new MemoryStream(await docx.Content.ReadAsByteArrayAsync()))
        using (var word = WordprocessingDocument.Open(stream, false))
        {
            word.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("30 days");
        }

        var pdf = await PostDocumentAsync(client, queryId, "pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        using (var pdfDocument = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync()))
        {
            pdfDocument.GetPage(1).Text.Should().Contain("30 days");
        }

        var txt = await PostDocumentAsync(client, queryId, "txt");
        txt.StatusCode.Should().Be(HttpStatusCode.OK);
        txt.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        Encoding
            .UTF8.GetString(await txt.Content.ReadAsByteArrayAsync())
            .Should()
            .Contain("30 days");

        var xlsx = await PostDocumentAsync(client, queryId, "xlsx");
        xlsx.StatusCode.Should().Be(HttpStatusCode.OK);
        xlsx.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using (var stream = new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()))
        using (var workbook = SpreadsheetDocument.Open(stream, false))
        {
            workbook
                .WorkbookPart!.WorksheetParts.First()
                .Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>()
                .Should()
                .NotBeEmpty();
        }
    }

    [Fact]
    public async Task Refusal_Returns422_NoDocumentAvailable()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        await SeedQueryAsync(
            queryId,
            sub,
            "Create a document about something absent.",
            "no relevant content found",
            Array.Empty<Guid>()
        );

        var response = await PostDocumentAsync(CreateBearerClient(token), queryId, "txt");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("no document available");
    }

    [Fact]
    public async Task CitationlessAnswer_Returns422()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        await SeedQueryAsync(
            queryId,
            sub,
            "Create a document about the policy.",
            "The policy is described here.",
            Array.Empty<Guid>()
        );

        var response = await PostDocumentAsync(CreateBearerClient(token), queryId, "txt");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task AnotherUsersQuery_Returns404()
    {
        var (ownerSub, _) = await ProvisionAndLoginAsync(UserRole.Employee);
        var (_, otherToken) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        await SeedChunksAsync(Guid.NewGuid(), new[] { (chunkId, 0, ChunkText) });
        await SeedQueryAsync(
            queryId,
            ownerSub,
            "Create a document.",
            $"Answer [{chunkId}]",
            new[] { chunkId }
        );

        var response = await PostDocumentAsync(CreateBearerClient(otherToken), queryId, "txt");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidFormat_Returns400()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        await SeedQueryAsync(queryId, sub, "prompt", "answer", Array.Empty<Guid>());

        var response = await PostDocumentAsync(CreateBearerClient(token), queryId, "pptx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownQuery_Returns404()
    {
        var (_, token) = await ProvisionAndLoginAsync(UserRole.Employee);

        var response = await PostDocumentAsync(CreateBearerClient(token), Guid.NewGuid(), "txt");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rendering_DoesNotWriteToTheContentStore()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        await SeedChunksAsync(Guid.NewGuid(), new[] { (chunkId, 0, ChunkText) });
        await SeedQueryAsync(
            queryId,
            sub,
            "Create a document about the refund policy.",
            $"Customers may return items within 30 days. [{chunkId}]",
            new[] { chunkId }
        );

        var before = Directory.Exists(_factory.ContentDirectory)
            ? Directory.GetFiles(_factory.ContentDirectory, "*", SearchOption.AllDirectories).Length
            : 0;

        var response = await PostDocumentAsync(CreateBearerClient(token), queryId, "docx");
        response.EnsureSuccessStatusCode();

        var after = Directory.Exists(_factory.ContentDirectory)
            ? Directory.GetFiles(_factory.ContentDirectory, "*", SearchOption.AllDirectories).Length
            : 0;
        after.Should().Be(before);
    }

    [Fact]
    public async Task TooLargeAnswer_Returns422()
    {
        var (sub, token) = await ProvisionAndLoginAsync(UserRole.Employee);
        var queryId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        await SeedChunksAsync(Guid.NewGuid(), new[] { (chunkId, 0, ChunkText) });
        await SeedQueryAsync(
            queryId,
            sub,
            "Create a text document with the whole answer.",
            new string('a', 26 * 1024 * 1024),
            new[] { chunkId }
        );

        var response = await PostDocumentAsync(CreateBearerClient(token), queryId, "txt");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("document too large");
    }

    private Task<HttpResponseMessage> PostDocumentAsync(
        HttpClient client,
        Guid queryId,
        string format
    ) => client.PostAsJsonAsync($"/api/queries/{queryId}/documents", new { format }, _jsonOptions);

    private HttpClient CreateBearerClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(string Sub, string Token)> ProvisionAndLoginAsync(UserRole role)
    {
        var username = $"doc-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword("doc-pass-1"),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
        await store.CreateAsync(user);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "doc-pass-1" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user.Id.ToString(), doc.RootElement.GetProperty("access_token").GetString()!);
    }

    private async Task SeedQueryAsync(
        Guid id,
        string sub,
        string prompt,
        string answer,
        IReadOnlyList<Guid> citationIds
    )
    {
        using var scope = _factory.Services.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQueryRepository>();
        await queries.AddAsync(
            new Query
            {
                Id = id,
                UserId = sub,
                Prompt = prompt,
                RetrievedChunkIds = citationIds,
                Answer = answer,
                CitationIds = citationIds,
                LatencyMs = 10,
                CreatedAt = DateTime.UtcNow,
            }
        );
    }

    private async Task SeedChunksAsync(
        Guid documentId,
        IEnumerable<(Guid Id, int Ordinal, string Text)> chunks
    )
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        using (var docCommand = connection.CreateCommand())
        {
            docCommand.CommandText =
                @"INSERT OR IGNORE INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedAt)
                  VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdAt)";
            docCommand.Parameters.AddWithValue("@id", documentId.ToString());
            docCommand.Parameters.AddWithValue("@filename", "document-doc.txt");
            docCommand.Parameters.AddWithValue("@mime", "text/plain");
            docCommand.Parameters.AddWithValue("@size", 64);
            docCommand.Parameters.AddWithValue("@hash", $"document-{documentId:N}");
            docCommand.Parameters.AddWithValue("@status", "Ready");
            docCommand.Parameters.AddWithValue("@createdBy", "seed");
            docCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
            await docCommand.ExecuteNonQueryAsync();
        }

        foreach (var chunk in chunks)
        {
            using var chunkCommand = connection.CreateCommand();
            chunkCommand.CommandText =
                @"INSERT OR IGNORE INTO Chunks (Id, DocumentId, Ordinal, Text, TokenCount)
                  VALUES (@id, @documentId, @ordinal, @text, @tokens);";
            chunkCommand.Parameters.AddWithValue("@id", chunk.Id.ToString());
            chunkCommand.Parameters.AddWithValue("@documentId", documentId.ToString());
            chunkCommand.Parameters.AddWithValue("@ordinal", chunk.Ordinal);
            chunkCommand.Parameters.AddWithValue("@text", chunk.Text);
            chunkCommand.Parameters.AddWithValue("@tokens", chunk.Text.Length);
            await chunkCommand.ExecuteNonQueryAsync();
        }
    }

    private FakeLlmClient GetLlmClient() =>
        (FakeLlmClient)_factory.Services.GetRequiredService<RAGGit.Core.Abstractions.ILlmClient>();
}
