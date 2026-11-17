using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// 034 US2: a source-backed creation returns the retrieved source and writes
/// nothing to query history or the library.
/// </summary>
public sealed class DocumentCreationSourceContractTests : IClassFixture<TestApiFactory>
{
    private const string ChunkText =
        "The refund policy allows returns within 30 days with a full refund.";

    private readonly TestApiFactory _factory;

    public DocumentCreationSourceContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Generate_SourceBackedRequest_UsesOnlyTheRetrievedSource_AndPersistsNothing()
    {
        var vectorStore = (FakeVectorStore)_factory.Services.GetRequiredService<IVectorStore>();
        vectorStore.Clear();

        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        vectorStore.Seed(
            new[]
            {
                new VectorRecord(
                    chunkId,
                    new float[384],
                    new Dictionary<string, object?>
                    {
                        ["documentId"] = documentId.ToString(),
                        ["text"] = ChunkText,
                        ["ordinal"] = 0,
                    }
                ),
            }
        );

        var documents = _factory.Services.GetRequiredService<IDocumentRepository>();
        await documents.AddAsync(
            new Document
            {
                Id = documentId,
                Filename = "refund-policy.txt",
                Mime = DocumentMimeType.Txt,
                Size = ChunkText.Length,
                Hash = Guid.NewGuid().ToString("N"),
                Status = DocumentStatus.Ready,
                CreatedBy = "contract",
                CreatedAt = DateTime.UtcNow,
            }
        );

        var llm = (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = true;
        llm.ResponseText = $"Refunds are available within 30 days. [{chunkId}]";

        var before = await CountRowsAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);

        var response = await client.PostAsJsonAsync(
            "/api/documents/generate",
            new
            {
                description = "Create a note based on the uploaded refund policy.",
                format = "docx",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response
            .Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
        using (var word = WordprocessingDocument.Open(stream, false))
        {
            var text = word.MainDocumentPart!.Document.Body!.InnerText;
            text.Should().Contain("30 days");
            text.Should().Contain("refund-policy.txt");
        }

        var after = await CountRowsAsync();
        after.Should().Be(before);
    }

    private async Task<(long Queries, long Documents)> CountRowsAsync()
    {
        var db = _factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT (SELECT COUNT(*) FROM Queries), (SELECT COUNT(*) FROM Documents);";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}
