using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Contract tests for GET /api/auth/me per contracts/api.yaml 1.1.0.
/// </summary>
public sealed class AuthContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public AuthContractTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAuthMe_AdminKey_Returns200WithAdminRole()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);

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
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("role").GetString().Should().Be("Employee");
        doc.RootElement.GetProperty("identityType").GetString().Should().Be("ApiKey");
    }

    [Fact]
    public async Task GetAuthMe_MissingKey_Returns401WithError()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task GetAuthMe_InvalidKey_Returns401WithError()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-key-xyz");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task GetAuthMe_ResponseToleratesUnknownExtraFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.AdminKey);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Simulate forward-compat: server may add extra fields; client must ignore them
        var extraJson = body.TrimEnd('}') + ",\"extraField\":\"ignored\",\"futureRole\":\"X\"}";
        var parsed = JsonSerializer.Deserialize<AuthMeResponse>(extraJson, options);
        parsed.Should().NotBeNull();
        parsed!.Role.Should().Be("Admin");
        parsed.IdentityType.Should().Be("ApiKey");

        // Also ensure real response deserializes ignoring unknowns
        var real = JsonSerializer.Deserialize<AuthMeResponse>(body, options);
        real.Should().NotBeNull();
        real!.Role.Should().BeOneOf("Admin", "Employee");
    }

    private sealed class AuthMeResponse
    {
        public string? IdentityType { get; set; }
        public string? Role { get; set; }
    }
}
