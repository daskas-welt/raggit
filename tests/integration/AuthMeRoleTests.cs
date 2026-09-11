using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T018: Role discovery + gating end-to-end (FR-004). Verifies GET /api/auth/me
/// returns identityType/role per 1.1.0 and Employee is blocked on upload/delete (403).
/// Also verifies AuthMe JSON round-trip ignores unknown fields (Q2 forward-compat).
/// </summary>
public sealed class AuthMeRoleTests
{
    [Fact]
    public async Task GetAuthMe_AdminKey_Returns200WithAdminRole()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("identityType").GetString().Should().Be("ApiKey");
        doc.RootElement.GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task GetAuthMe_EmployeeKey_Returns200WithEmployeeRole()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("identityType").GetString().Should().Be("ApiKey");
        doc.RootElement.GetProperty("role").GetString().Should().Be("Employee");
    }

    [Fact]
    public async Task Employee_PostDocument_Returns403()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("Employee upload attempt content.");
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "employee-upload.txt");

        var response = await client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Employee_DeleteDocument_Returns403()
    {
        using var factory = new IntegrationTestFactory();

        // Admin creates a document
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
        var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes("RBAC delete test content.");
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "rbac-delete.txt");
        var upload = await adminClient.PostAsync("/api/documents", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploadBody = await upload.Content.ReadAsStringAsync();
        var docId = JsonDocument.Parse(uploadBody).RootElement.GetProperty("id").GetString();
        docId.Should().NotBeNullOrEmpty();

        // Employee attempts delete
        using var employeeClient = factory.CreateClient();
        employeeClient.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        var delete = await employeeClient.DeleteAsync($"/api/documents/{docId}");

        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthMe_JsonIgnoresUnknownFields_ForwardCompat()
    {
        using var factory = new IntegrationTestFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        // Simulate server adding extra fields in future (004); client must ignore them.
        var extraJson = body.TrimEnd('}') + ",\"extraField\":\"ignored\",\"futureRole\":\"X\"}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<AuthMeDto>(extraJson, options);
        parsed.Should().NotBeNull();
        parsed!.Role.Should().Be("Admin");
        parsed.IdentityType.Should().Be("ApiKey");

        var real = JsonSerializer.Deserialize<AuthMeDto>(body, options);
        real.Should().NotBeNull();
        real!.Role.Should().BeOneOf("Admin", "Employee");
    }

    private sealed class AuthMeDto
    {
        public string? IdentityType { get; set; }
        public string? Role { get; set; }
    }
}
