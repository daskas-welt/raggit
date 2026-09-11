using System;
using System.Collections.Generic;
using System.Data;
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
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Integration tests for document deletion: SQLite + LanceDB purge and
/// subsequent query exclusion. Uses the real Workstation.Api with a temp
/// LanceDB vector store and deterministic fakes for Ollama.
/// </summary>
public sealed class DeleteIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Admin_DeleteDocument_PurgesSQLiteAndLanceDB_And_QueryExcludes()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        const string uniqueTerm = "xyz-unique-delete-term-42";
        var uploaded = await UploadTextAsync(
            adminClient,
            "purge-test.txt",
            $"This document contains the {uniqueTerm} marker."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(uploaded);

        var delete = await adminClient.DeleteAsync($"/api/documents/{document.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AssertDocumentRemovedFromSQLiteAsync(factory, document.Id);
        await AssertLanceDBPurgedAsync(factory, document.Id);

        var queryResponse = await employeeClient.PostAsJsonAsync(
            "/api/query",
            new { query = uniqueTerm },
            JsonOptions
        );
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await queryResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        json.GetProperty("answer").GetString().Should().Be("no relevant content found");
        json.GetProperty("citations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Employee_DeleteDocument_Returns403()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        var uploaded = await UploadTextAsync(
            adminClient,
            "employee-delete-forbidden.txt",
            "Employee cannot delete."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(uploaded);

        var response = await employeeClient.DeleteAsync($"/api/documents/{document.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static HttpClient CreateAdminClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        return client;
    }

    private static HttpClient CreateEmployeeClient(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        return client;
    }

    private static async Task<HttpResponseMessage> UploadTextAsync(
        HttpClient client,
        string filename,
        string text
    )
    {
        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(text);
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", filename);
        return await client.PostAsync("/api/documents", form);
    }

    private static async Task AssertDocumentRemovedFromSQLiteAsync(
        IntegrationTestFactory factory,
        Guid documentId
    )
    {
        var db = factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();

        using var documentCommand = connection.CreateCommand();
        documentCommand.CommandText = "SELECT COUNT(*) FROM Documents WHERE Id = @id;";
        documentCommand.Parameters.AddWithValue("@id", documentId.ToString());
        var documentCount = Convert.ToInt64(await documentCommand.ExecuteScalarAsync());
        documentCount.Should().Be(0, "Documents row should be removed after delete");

        using var chunkCommand = connection.CreateCommand();
        chunkCommand.CommandText = "SELECT COUNT(*) FROM Chunks WHERE DocumentId = @id;";
        chunkCommand.Parameters.AddWithValue("@id", documentId.ToString());
        var chunkCount = Convert.ToInt64(await chunkCommand.ExecuteScalarAsync());
        chunkCount.Should().Be(0, "Chunks rows should be removed after delete");
    }

    private static async Task AssertLanceDBPurgedAsync(
        IntegrationTestFactory factory,
        Guid documentId
    )
    {
        var store = factory.Services.GetRequiredService<IVectorStore>();
        var hits = await store.SearchAsync(
            factory.QueryVector,
            limit: 100,
            documentIdFilter: documentId.ToString()
        );
        hits.Count.Should().Be(0, "LanceDB vectors for documentId should be purged after delete");
    }

    private static async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document.");
    }
}
