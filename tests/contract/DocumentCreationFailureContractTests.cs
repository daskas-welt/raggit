using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// 034 US3: a failed creation is a user-safe JSON error, never a file, and no
/// query-history or generated-document row is written.
/// </summary>
public sealed class DocumentCreationFailureContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public DocumentCreationFailureContractTests(TestApiFactory factory)
    {
        _factory = factory;
        var llm = (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
        llm.ChatException = null;
    }

    [Fact]
    public async Task Generate_ModelUnavailable_Returns503WithoutAFile()
    {
        var llm = (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = false;
        var before = await CountRowsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        var response = await client.PostAsJsonAsync(
            "/api/documents/generate",
            new { description = "Draft a welcome note.", format = "txt" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().Contain("AI workstation unavailable");
        (await CountRowsAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Generate_ModelChatFailure_Returns503WithoutAFile()
    {
        var llm = (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
        llm.Healthy = true;
        llm.ChatException = new HttpRequestException("model unavailable offline");
        var before = await CountRowsAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        var response = await client.PostAsJsonAsync(
            "/api/documents/generate",
            new { description = "Draft a welcome note.", format = "txt" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentDisposition.Should().BeNull();
        (await CountRowsAsync()).Should().Be(before);
    }

    private async Task<(long Queries, long Documents)> CountRowsAsync()
    {
        var db = _factory.Services.GetRequiredService<RagDbContext>();
        await using var connection = db.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT (SELECT COUNT(*) FROM Queries), (SELECT COUNT(*) FROM Documents);";
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt64(0), reader.GetInt64(1));
    }
}
