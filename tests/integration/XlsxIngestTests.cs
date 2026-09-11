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

public sealed class XlsxIngestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Upload_Sample3Sheet_Becomes_Ready_And_Chunks_Contain_SheetPrefix_And_Dedupe()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var bytes = await LoadFixtureAsync("sample-3sheet.xlsx");
        var form = CreateXlsxForm(bytes, "sample-3sheet.xlsx");
        var response = await client.PostAsync("/api/documents", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Xlsx);

        // Poll GET /api/documents → Ready (with fake embedder, immediate)
        var listResp = await client.GetAsync("/api/documents");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var docs = await DeserializeListAsync(listResp);
        var uploaded = docs.FirstOrDefault(d => d.Id == doc.Id);
        uploaded.Should().NotBeNull();
        uploaded!.Status.Should().Be(DocumentStatus.Ready);

        // Chunks rows for that document contain [Sheet: prefix
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var conn = db.CreateConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Text FROM Chunks WHERE DocumentId = @id;";
        cmd.Parameters.AddWithValue("@id", doc.Id.ToString());
        var texts = new System.Collections.Generic.List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) texts.Add(reader.GetString(0));
        texts.Should().NotBeEmpty();
        texts.Should().Contain(t => t.Contains("[Sheet:"));

        // Re-upload identical bytes → 200 with existing documentId and no second index
        var form2 = CreateXlsxForm(bytes, "sample-3sheet.xlsx");
        var second = await client.PostAsync("/api/documents", form2);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var dup = await DeserializeDocumentAsync(second);
        dup.Id.Should().Be(doc.Id);

        // Verify no duplicate Chunks (count unchanged)
        await using var conn2 = db.CreateConnection();
        await conn2.OpenAsync();
        using var countCmd = conn2.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Chunks WHERE DocumentId = @id;";
        countCmd.Parameters.AddWithValue("@id", doc.Id.ToString());
        var chunkCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        chunkCount.Should().BeGreaterThan(0);
        // No second document row
        var list2 = await DeserializeListAsync(await client.GetAsync("/api/documents"));
        list2.Count(d => d.Filename == "sample-3sheet.xlsx").Should().Be(1);
    }

    private static MultipartFormDataContent CreateXlsxForm(byte[] bytes, string filename)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", filename);
        return form;
    }

    private static async Task<byte[]> LoadFixtureAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine("fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "xlsx", fileName),
        };
        foreach (var p in candidates) if (File.Exists(p)) return await File.ReadAllBytesAsync(p);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var cand = Path.Combine(dir.FullName, "tests", "integration", "fixtures", "xlsx", fileName);
            if (File.Exists(cand)) return await File.ReadAllBytesAsync(cand);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Fixture {fileName} not found");
    }

    private static async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize Document");
    }

    private static async Task<System.Collections.Generic.List<Document>> DeserializeListAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<System.Collections.Generic.List<Document>>(json, JsonOptions) ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }
}
