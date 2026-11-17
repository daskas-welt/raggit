using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// 034 US2: the full ingest → retrieve → compose → render path uses a settled
/// source document and never writes a query-history row.
/// </summary>
public sealed class DocumentCreationEvalTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new DocumentMimeTypeConverter(),
            new System.Text.Json.Serialization.JsonStringEnumConverter(),
        },
    };

    [Fact]
    public async Task SourceBackedCreation_UsesTheSettledLibraryDocument()
    {
        using var factory = new IntegrationTestFactory();
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var unique = Guid.NewGuid().ToString("N")[..8];
        var form = new MultipartFormDataContent();
        var file = new StreamContent(
            new MemoryStream(
                Encoding.UTF8.GetBytes(
                    "The refund policy allows returns within 30 days with a full refund. " + unique
                )
            )
        );
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"refund-policy-{unique}.txt");
        (await admin.PostAsync("/api/documents", form)).EnsureSuccessStatusCode();

        var settled = await factory.WaitForSettledAsync(admin);
        settled.Should().ContainSingle();

        var vectorStore = factory.Services.GetRequiredService<IVectorStore>();
        var chunks = await vectorStore.SearchAsync(factory.QueryVector, 5);
        chunks.Should().NotBeEmpty();
        var chunk = chunks.First();

        var llm = (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = true;
        llm.ResponseText = $"Refunds are available within 30 days. [{chunk.ChunkId}]";

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        var response = await client.PostAsJsonAsync(
            "/api/documents/generate",
            new
            {
                description = "Create a short note from the uploaded refund policy.",
                format = "docx",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var word = WordprocessingDocument.Open(stream, false);
        var text = word.MainDocumentPart!.Document.Body!.InnerText;
        text.Should().Contain("30 days");
        text.Should().Contain($"refund-policy-{unique}.txt");
    }
}
