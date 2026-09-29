using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// RBAC integration tests for Employee read-only library browsing with the
/// real Workstation.Api, LanceDB vector store, and deterministic fakes.
/// </summary>
public sealed class RbacIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Employee_BrowseReadOnly_AdminCanUploadAndDelete()
    {
        using var factory = new IntegrationTestFactory();
        var adminClient = CreateAdminClient(factory);
        var employeeClient = CreateEmployeeClient(factory);

        var uploaded = await UploadTextAsync(
            adminClient,
            "rbac-integration.txt",
            "RBAC integration read-only content."
        );
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await DeserializeDocumentAsync(uploaded);

        var adminList = await adminClient.GetAsync("/api/documents");
        adminList.StatusCode.Should().Be(HttpStatusCode.OK);
        // Background ingest: wait for the worker to settle before reading the two
        // lists, or one of them can be captured mid-transition (Uploading vs Ready).
        var adminDocuments = await factory.WaitForSettledAsync(adminClient);
        adminDocuments.Should().Contain(d => d.Status == DocumentStatus.Ready);

        var employeeList = await employeeClient.GetAsync("/api/documents");
        employeeList.StatusCode.Should().Be(HttpStatusCode.OK);
        var employeeDocuments = await DeserializeListAsync(employeeList);

        employeeDocuments
            .Should()
            .BeEquivalentTo(adminDocuments, options => options.WithStrictOrdering());
        employeeDocuments.Should().Contain(d => d.Id == document.Id);

        var employeeUpload = await UploadTextAsync(
            employeeClient,
            "employee-upload.txt",
            "Employee upload attempt."
        );
        employeeUpload.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var employeeDelete = await employeeClient.DeleteAsync($"/api/documents/{document.Id}");
        employeeDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminDelete = await adminClient.DeleteAsync($"/api/documents/{document.Id}");
        adminDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
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

    private static async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document.");
    }

    private static async Task<List<Document>> DeserializeListAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Document>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize document list.");
    }
}
