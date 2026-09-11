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
/// RBAC contract tests per FR-003 SC-005: Employee can browse read-only,
/// but upload and delete are forbidden.
/// </summary>
public sealed class RbacContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public RbacContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Employee_GetDocuments_Returns200_SameListAsAdmin()
    {
        var adminClient = CreateAdminClient();
        var uploaded = await UploadTextAsync(
            adminClient,
            "rbac-list.txt",
            "RBAC list contract content."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);

        var adminList = await adminClient.GetAsync("/api/documents");
        adminList.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminDocuments = await DeserializeListAsync(adminList);

        var employeeClient = CreateEmployeeClient();
        var employeeList = await employeeClient.GetAsync("/api/documents");
        employeeList.StatusCode.Should().Be(HttpStatusCode.OK);
        var employeeDocuments = await DeserializeListAsync(employeeList);

        employeeDocuments
            .Should()
            .BeEquivalentTo(adminDocuments, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Employee_PostDocument_Returns403()
    {
        var employeeClient = CreateEmployeeClient();
        var response = await UploadTextAsync(
            employeeClient,
            "employee-upload.txt",
            "Employee upload attempt."
        );
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_DeleteDocument_Returns403()
    {
        var adminClient = CreateAdminClient();
        var uploaded = await UploadTextAsync(
            adminClient,
            "rbac-delete.txt",
            "RBAC delete contract content."
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
