using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Auth;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T039: WAN-disabled identity leg — provision → login → me → attributed upload → cited query.
/// Uses deterministic fakes for Ollama/embed so the test requires zero egress beyond loopback.
/// </summary>
public sealed class OfflineIdentityTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task OfflineIdentity_FullPersonSession_ProvisionLoginMeUploadQuery_WithCitations()
    {
        using var factory = new IntegrationTestFactory();

        // Provision an Admin and an Employee directly on the workstation (operator CLI simulation).
        var (admin, adminPassword) = await ProvisionPersonAsync(
            factory,
            "offline-ada",
            "Offline Ada",
            UserRole.Admin
        );
        var (employee, employeePassword) = await ProvisionPersonAsync(
            factory,
            "offline-bob",
            "Offline Bob",
            UserRole.Employee
        );

        // Admin login and identity discovery.
        var adminClient = factory.CreateClient();
        var adminToken = await LoginAsync(adminClient, admin.Username, adminPassword);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            adminToken
        );

        var adminMe = await adminClient.GetAsync("/api/auth/me");
        adminMe.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminMeJson = await adminMe.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        adminMeJson.GetProperty("identityType").GetString().Should().Be("Local");
        adminMeJson.GetProperty("role").GetString().Should().Be("Admin");
        adminMeJson.GetProperty("sub").GetString().Should().Be(admin.Id.ToString());

        // Admin uploads a document.
        var upload = await UploadTextAsync(
            adminClient,
            "offline-person.txt",
            "Refunds accepted within 30 days."
        );
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await upload.Content.ReadFromJsonAsync<Document>(_jsonOptions);
        document.Should().NotBeNull();
        document!.CreatedBy.Should().Be(admin.Id.ToString());

        // Employee login and cited query — entirely local.
        var employeeClient = factory.CreateClient();
        var employeeToken = await LoginAsync(employeeClient, employee.Username, employeePassword);
        employeeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            employeeToken
        );

        var employeeMe = await employeeClient.GetAsync("/api/auth/me");
        employeeMe.StatusCode.Should().Be(HttpStatusCode.OK);
        var employeeMeJson = await employeeMe.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        employeeMeJson.GetProperty("identityType").GetString().Should().Be("Local");
        employeeMeJson.GetProperty("role").GetString().Should().Be("Employee");

        var llm = (FakeLlmClient)factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = true;
        llm.ResponseText = "Refunds are accepted within 30 days.";

        var query = await employeeClient.PostAsJsonAsync(
            "/api/queries",
            new { query = "refund policy" },
            _jsonOptions
        );
        query.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = await query.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        answer.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
        answer.GetProperty("citations").GetArrayLength().Should().BeGreaterThan(0);
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string username,
        string password
    )
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument
                .Parse(await login.Content.ReadAsStringAsync())
                .RootElement.GetProperty("access_token")
                .GetString()
            ?? throw new InvalidOperationException("Missing access_token.");
    }

    private static async Task<(User User, string Password)> ProvisionPersonAsync(
        IntegrationTestFactory factory,
        string username,
        string displayName,
        UserRole role
    )
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<UserStore>();
        var password = $"{username}-secure-pass-1";
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = displayName,
            Role = role,
            PasswordHash = PasswordHasher.HashPassword(password),
            IsActive = true,
            FailedAccessCount = 0,
            LockoutUntil = null,
            MustChangePassword = false,
            LastSignInAt = null,
            LastPasswordChangedAt = now,
            CreatedAt = now,
        };
        await store.CreateAsync(user);
        return (user, password);
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
}
