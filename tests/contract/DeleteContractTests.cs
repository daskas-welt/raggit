using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Contract tests for <c>DELETE /api/documents/{id}</c> per contracts/api.yaml.
/// Runs against the full Workstation.Api via <see cref="WebApplicationFactory{Program}"/>
/// with deterministic fakes so the tests do not require Ollama/LanceDB.
/// </summary>
public sealed class DeleteContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public DeleteContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_DeleteExistingDocument_Returns204_And_GetExcludesIt()
    {
        var adminClient = CreateAdminClient();

        var uploaded = await UploadTextAsync(
            adminClient,
            "delete-contract.txt",
            "Delete contract unique content."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(uploaded);

        var beforeList = await adminClient.GetAsync("/api/documents");
        beforeList.StatusCode.Should().Be(HttpStatusCode.OK);
        var before = await DeserializeListAsync(beforeList);

        var delete = await adminClient.DeleteAsync($"/api/documents/{document.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterList = await adminClient.GetAsync("/api/documents");
        afterList.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await DeserializeListAsync(afterList);

        after.Should().HaveCount(before.Count - 1);
        after.Should().NotContain(d => d.Id == document.Id);
    }

    [Fact]
    public async Task Admin_DeleteUnknownDocument_Returns404()
    {
        var adminClient = CreateAdminClient();
        var unknownId = Guid.NewGuid();

        var response = await adminClient.DeleteAsync($"/api/documents/{unknownId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Employee_DeleteDocument_Returns403()
    {
        var adminClient = CreateAdminClient();
        var uploaded = await UploadTextAsync(
            adminClient,
            "delete-employee-forbidden.txt",
            "Employee delete forbidden content."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(uploaded);

        var employeeClient = CreateEmployeeClient();
        var response = await employeeClient.DeleteAsync($"/api/documents/{document.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);
        return client;
    }

    private HttpClient CreateEmployeeClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
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

    private async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document response.");
    }

    private async Task<List<Document>> DeserializeListAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Document>>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }
}
