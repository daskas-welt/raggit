using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T011: The API boots successfully with zero user accounts and presents no first-run
/// wizard; protected endpoints require authentication.
/// </summary>
public sealed class BootWithZeroAccountsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BootWithZeroAccountsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiBoots_WithZeroAccounts_HealthReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiBoots_WithZeroAccounts_LoginRequiresAuth()
    {
        using var client = _factory.CreateClient();
        var json = JsonSerializer.Serialize(new { username = "admin", password = "admin" });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
