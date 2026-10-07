using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Workstation.Api;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T019: A pre-feature library (Ready documents with child chunks only) is
/// upgraded to the two-level structure — every Ready document gains parent
/// rows, no document is lost or duplicated, and a second backfill run
/// processes nothing (idempotent).
/// </summary>
public sealed class ReindexBackfillTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Backfill_UpgradesStaleReadyDocuments_Idempotent()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);

        var staged = await UploadTextAsync(
            adminClient,
            "The refund policy allows returns within 30 days."
        );
        var settled = await factory.WaitForSettledAsync(adminClient);
        settled.Single(d => d.Id == staged.Id).Status.Should().Be(DocumentStatus.Ready);
        var beforeIds = settled.Select(d => d.Id).OrderBy(id => id).ToList();

        // Simulate a pre-feature library: drop every parent row.
        await DeleteParentRowsAsync(factory);

        var repository = factory.Services.GetRequiredService<IDocumentRepository>();
        var stale = await repository.ListReadyDocumentIdsWithoutParentChunkAsync();
        stale.Should().Contain(staged.Id);

        var backfill = factory
            .Services.GetServices<IHostedService>()
            .OfType<ReindexBackfillService>()
            .Single();
        var requeued = await backfill.BackfillAsync();
        requeued.Should().Be(1);

        var resettled = await factory.WaitForSettledAsync(adminClient);
        var resettledIds = resettled.Select(d => d.Id).OrderBy(id => id).ToList();
        resettledIds.Should().Equal(beforeIds, "no document lost or duplicated");
        resettled.Should().OnlyContain(d => d.Status == DocumentStatus.Ready);

        // Every Ready document now has parent rows.
        foreach (var document in resettled)
        {
            (await CountParentRowsAsync(factory, document.Id))
                .Should()
                .BeGreaterThan(0, $"document {document.Id} must be two-level");
            (await ListChunkLevelsAsync(factory, document.Id))
                .Should()
                .Contain(ChunkLevel.Child)
                .And.Contain(ChunkLevel.Parent);
        }

        // Second run is a no-op.
        var second = await backfill.BackfillAsync();
        second.Should().Be(0);
        (await factory.WaitForSettledAsync(adminClient))
            .Select(d => d.Id)
            .OrderBy(id => id)
            .Should()
            .Equal(beforeIds);
    }

    private static async Task<Document> UploadTextAsync(HttpClient adminClient, string text)
    {
        var form = new MultipartFormDataContent();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes($"{text} {unique}")));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", $"backfill-{unique}.txt");

        var response = await adminClient.PostAsync("/api/documents", form);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var staged = await response.Content.ReadFromJsonAsync<Document>(JsonOptions);
        staged.Should().NotBeNull();
        return staged!;
    }

    private static HttpClient CreateAdminClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        return client;
    }

    private static async Task DeleteParentRowsAsync(IntegrationTestFactory factory)
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Chunks WHERE Level = @parent;";
        command.Parameters.AddWithValue("@parent", (int)ChunkLevel.Parent);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountParentRowsAsync(
        IntegrationTestFactory factory,
        Guid documentId
    )
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM Chunks WHERE DocumentId = @id AND Level = @parent;";
        command.Parameters.AddWithValue("@id", documentId.ToString());
        command.Parameters.AddWithValue("@parent", (int)ChunkLevel.Parent);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<IReadOnlyList<ChunkLevel>> ListChunkLevelsAsync(
        IntegrationTestFactory factory,
        Guid documentId
    )
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT Level FROM Chunks WHERE DocumentId = @id;";
        command.Parameters.AddWithValue("@id", documentId.ToString());
        var levels = new List<ChunkLevel>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            levels.Add((ChunkLevel)reader.GetInt32(0));
        }

        return levels;
    }
}
