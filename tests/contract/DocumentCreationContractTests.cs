using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// 034 US1: create a new document from a description in each supported format.
/// Validation and authorization are enforced before any model work.
/// </summary>
public sealed class DocumentCreationContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public DocumentCreationContractTests(TestApiFactory factory)
    {
        _factory = factory;
        GetLlmClient().ResponseText = "Welcome to the team.";
        GetLlmClient().Healthy = true;
        GetLlmClient().ChatException = null;
    }

    [Fact]
    public async Task Generate_WithoutCredentials_Returns401()
    {
        var response = await _factory
            .CreateClient()
            .PostAsJsonAsync(
                "/api/documents/generate",
                new { description = "Draft a welcome note.", format = "txt" }
            );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Generate_BlankDescription_Returns400(string description)
    {
        var response = await GenerateAsync(description, "txt");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_OverlongDescription_Returns400()
    {
        var response = await GenerateAsync(new string('x', 10_001), "txt");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Generate_UnsupportedFormat_Returns400()
    {
        var response = await GenerateAsync("Draft a welcome note.", "pptx");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should()
            .Contain("docx")
            .And.Contain("xlsx")
            .And.Contain("pdf")
            .And.Contain("txt");
    }

    [Theory]
    [InlineData("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("pdf", "application/pdf")]
    [InlineData("txt", "text/plain")]
    public async Task Generate_ValidRequest_ReturnsTheSelectedFormat(
        string format,
        string expectedContentType
    )
    {
        var response = await GenerateAsync("Draft a welcome note for a new team member.", format);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(expectedContentType);
        response.Content.Headers.ContentDisposition.Should().NotBeNull();
        response
            .Content.Headers.ContentDisposition!.FileNameStar.Should()
            .EndWith($".{format}")
            .And.Contain("welcome");
        (await response.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
    }

    private async Task<HttpResponseMessage> GenerateAsync(string description, string format)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", _factory.EmployeeKey);
        return await client.PostAsJsonAsync("/api/documents/generate", new { description, format });
    }

    private FakeLlmClient GetLlmClient() =>
        (FakeLlmClient)_factory.Services.GetRequiredService<ILlmClient>();
}
