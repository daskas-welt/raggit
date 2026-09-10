using System;
using System.IO;
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
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using RAGGit.Ingest.Vector;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Edge-case integration tests per spec.md: large batches, duplicate hash,
/// model unavailable, LAN partition retry, and restart persistence (FR-008).
/// </summary>
public sealed class EdgeCaseTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Upload_ManyConcurrentFiles_AllReady()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        const int count = 20;
        var tasks = new Task<HttpResponseMessage>[count];
        for (var i = 0; i < count; i++)
        {
            tasks[i] = UploadTextAsync(client, $"bulk-{i}.txt", $"Bulk document {i} with enough text to chunk.");
        }

        var responses = await Task.WhenAll(tasks);
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var listResponse = await client.GetAsync("/api/documents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var documents = await DeserializeListAsync(listResponse);
        documents.Should().HaveCount(count);
        documents.Should().OnlyContain(d => d.Status == DocumentStatus.Ready);
    }

    [Fact]
    public async Task Upload_DuplicateHash_DoesNotReindex()
    {
        using var factory = new IntegrationTestFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        const string content = "Duplicate hash edge case content.";
        var first = await UploadTextAsync(client, "dup.txt", content);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDoc = await DeserializeDocumentAsync(first);

        var second = await UploadTextAsync(client, "renamed.txt", content);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondDoc = await DeserializeDocumentAsync(second);

        secondDoc.Id.Should().Be(firstDoc.Id);
        secondDoc.Hash.Should().Be(firstDoc.Hash);
    }

    [Fact]
    public async Task Query_LlmUnavailable_ReturnsModelUnavailableOffline()
    {
        using var factory = new IntegrationTestFactory();
        var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        await UploadTextAsync(adminClient, "offline.txt", "Offline content for edge case.");

        var llm = (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = false;

        var response = await employeeClient.PostAsJsonAsync("/api/query", new { query = "offline content" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AI workstation unavailable");
    }

    [Fact]
    public async Task Query_LanPartition_NoCloudFallback_ReturnsServiceUnavailable()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        await UploadTextAsync(adminClient, "lan-partition.txt", "Content before partition.");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        // Simulate total AI unavailability (vector + LLM down); there must be
        // no cloud fallback path.
        var vectorStore = (LanceDbLocalClient)factory.Services.GetRequiredService<IVectorStore>();
        var blockedPath = Path.Combine(vectorStore.StoragePath, "..", "blocked");
        Directory.CreateDirectory(blockedPath);
        Directory.Move(vectorStore.StoragePath, Path.Combine(blockedPath, "lancedb"));

        var response = await client.PostAsJsonAsync("/api/query", new { query = "anything" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task RestartPersistence_DocumentAndQuerySurviveNewFactory()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "raggit-restart-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(baseDir);
        var dbPath = Path.Combine(baseDir, "rag.db");
        var lanceDbPath = Path.Combine(baseDir, "lancedb");

        Document persistedDocument;

        // First "session": index a document and query it.
        using (var factory1 = new RestartableFactory(dbPath, lanceDbPath))
        {
            var adminClient = factory1.CreateClient();
            adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory1.AdminKey);

            var upload = await UploadTextAsync(adminClient, "persist.txt", "Persisted content for restart test.");
            upload.StatusCode.Should().Be(HttpStatusCode.Created);
            persistedDocument = await DeserializeDocumentAsync(upload);

            var employeeClient = factory1.CreateClient();
            employeeClient.DefaultRequestHeaders.Add("X-Api-Key", factory1.EmployeeKey);
            var queryResponse = await employeeClient.PostAsJsonAsync("/api/query", new { query = "Persisted content" }, JsonOptions);
            queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Second "session": new process, same disk paths.
        using (var factory2 = new RestartableFactory(dbPath, lanceDbPath))
        {
            var adminClient = factory2.CreateClient();
            adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory2.AdminKey);

            var listResponse = await adminClient.GetAsync("/api/documents");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var documents = await DeserializeListAsync(listResponse);
            documents.Should().Contain(d => d.Id == persistedDocument.Id && d.Status == DocumentStatus.Ready);

            var employeeClient = factory2.CreateClient();
            employeeClient.DefaultRequestHeaders.Add("X-Api-Key", factory2.EmployeeKey);
            var queryResponse = await employeeClient.PostAsJsonAsync("/api/query", new { query = "Persisted content" }, JsonOptions);
            queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
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

    private static async Task<System.Collections.Generic.List<Document>> DeserializeListAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<System.Collections.Generic.List<Document>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }

    /// <summary>
    /// Factory that reuses fixed SQLite/LanceDB paths to simulate a workstation
    /// restart (new process, same persisted data) per FR-008.
    /// </summary>
    private sealed class RestartableFactory : IntegrationTestFactory
    {
        public RestartableFactory(string dbPath, string lanceDbPath)
            : base(dbPath, lanceDbPath)
        {
        }
    }
}
