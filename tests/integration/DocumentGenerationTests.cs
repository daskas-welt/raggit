using System;
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
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Auth;
using RAGGit.Core.Models;
using UglyToad.PdfPig;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// 033: end-to-end ask → render for every format, using the real pipeline and
/// the real upload/ingest path with deterministic fakes.
/// </summary>
public sealed class DocumentGenerationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task AskThenRender_AllFourFormats_ReadBack()
    {
        using var factory = new IntegrationTestFactory();
        var (_, token) = await ProvisionAndLoginAsync(factory);
        await SeedRefundPolicyDocumentAsync(factory);

        var llm = GetLlmClient(factory);
        llm.Healthy = true;
        llm.ResponseText = "Refunds are accepted within 30 days.";

        var client = CreateBearerClient(factory, token);
        var ask = await client.PostAsJsonAsync(
            "/api/queries",
            new { query = "Create a document about the refund policy." },
            JsonOptions
        );
        ask.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ask.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var queryId = json.GetProperty("queryId").GetString();
        queryId.Should().NotBeNullOrWhiteSpace();

        // Rendering must not need the model to be healthy.
        llm.Healthy = false;

        var docx = await RenderAsync(client, queryId!, "docx");
        docx.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var stream = new MemoryStream(await docx.Content.ReadAsByteArrayAsync()))
        using (var word = WordprocessingDocument.Open(stream, false))
        {
            word.MainDocumentPart!.Document.Body!.InnerText.Should().Contain("30 days");
        }

        var xlsx = await RenderAsync(client, queryId!, "xlsx");
        xlsx.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var stream = new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()))
        using (var workbook = SpreadsheetDocument.Open(stream, false))
        {
            workbook
                .WorkbookPart!.WorksheetParts.First()
                .Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>()
                .Should()
                .NotBeEmpty();
        }

        var pdf = await RenderAsync(client, queryId!, "pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var document = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync()))
        {
            document.GetPage(1).Text.Should().Contain("30 days");
        }

        var txt = await RenderAsync(client, queryId!, "txt");
        txt.StatusCode.Should().Be(HttpStatusCode.OK);
        Encoding
            .UTF8.GetString(await txt.Content.ReadAsByteArrayAsync())
            .Should()
            .Contain("30 days");
    }

    private static Task<HttpResponseMessage> RenderAsync(
        HttpClient client,
        string queryId,
        string format
    ) => client.PostAsJsonAsync($"/api/queries/{queryId}/documents", new { format }, JsonOptions);

    private static HttpClient CreateBearerClient(IntegrationTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<(string Sub, string Token)> ProvisionAndLoginAsync(
        IntegrationTestFactory factory
    )
    {
        var username = $"doc-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            Role = UserRole.Employee,
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

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = "doc-pass-1" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (user.Id.ToString(), doc.RootElement.GetProperty("access_token").GetString()!);
    }

    private static async Task SeedRefundPolicyDocumentAsync(IntegrationTestFactory factory)
    {
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var bytes = Encoding.UTF8.GetBytes(
            $"The refund policy allows returns within 30 days with a full refund. {unique}"
        );
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"refund-policy-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        staged.Should().NotBeNull();

        var settled = await factory.WaitForSettledAsync(adminClient);
        var indexed = settled.Single(document => document.Id == staged!.Id);
        indexed
            .Status.Should()
            .Be(DocumentStatus.Ready, $"seeding failed: {indexed.FailureReason}");
    }

    private static FakeLlmClient GetLlmClient(IntegrationTestFactory factory) =>
        (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
}
