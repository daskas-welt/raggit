using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 034 US1: document generation is a deliberate source-free composition path,
/// not the Ask refusal path. The local model still does the work.
/// </summary>
public sealed class DocumentGenerationServiceTests
{
    [Fact]
    public async Task GenerateDocumentAsync_WithoutChunks_ComposesFromDescription()
    {
        var llm = new RecordingLlmClient("Welcome to the team.");
        var service = new GenerationService(llm);

        var (body, citations) = await service.GenerateDocumentAsync(
            "Draft a welcome note for a new team member.",
            DocumentOutputFormat.Docx,
            System.Array.Empty<SearchResult>()
        );

        body.Should().Be("Welcome to the team.");
        citations.Should().BeEmpty();
        llm.Calls.Should().ContainSingle();
        llm.Calls[0].UserPrompt.Should().Contain("Draft a welcome note for a new team member.");
        llm.Calls[0].SystemPrompt.Should().Contain("document");
        llm.Calls[0].SystemPrompt.Should().Contain("RAGGit sources");
        llm.Calls[0].SystemPrompt.Should().NotContain("title line");
    }

    [Fact]
    public async Task GenerateDocumentAsync_UsesTheSelectedFormatInThePrompt()
    {
        var llm = new RecordingLlmClient("A table.");
        var service = new GenerationService(llm);

        await service.GenerateDocumentAsync(
            "List the policy areas.",
            DocumentOutputFormat.Xlsx,
            System.Array.Empty<SearchResult>()
        );

        llm.Calls.Single().SystemPrompt.Should().Contain("spreadsheet");
    }

    [Fact]
    public async Task GenerateDocumentAsync_EmptyChunks_DoesNotReturnTheAskRefusal()
    {
        var service = new GenerationService(new RecordingLlmClient("A real document."));

        var (body, _) = await service.GenerateDocumentAsync(
            "Draft a note.",
            DocumentOutputFormat.Txt,
            System.Array.Empty<SearchResult>()
        );

        body.Should().NotBe("no relevant content found");
    }

    private sealed class RecordingLlmClient(string response) : ILlmClient
    {
        public List<(string SystemPrompt, string UserPrompt)> Calls { get; } = new();

        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        )
        {
            Calls.Add((systemPrompt, userPrompt));
            return Task.FromResult(response);
        }

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
