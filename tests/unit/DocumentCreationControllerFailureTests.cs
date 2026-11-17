using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;
using RAGGit.Documents;
using RAGGit.Retrieval;
using RAGGit.Workstation.Api.Controllers;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 034 US3: a renderer failure becomes a safe 500 with no file response.
/// </summary>
public sealed class DocumentCreationControllerFailureTests
{
    [Fact]
    public async Task Generate_RendererFailure_Returns500WithoutAFile()
    {
        var controller = BuildController();

        var result = await controller.Generate(
            new DocumentCreationRequest
            {
                Description = "Draft a welcome note.",
                Format = "txt",
            },
            CancellationToken.None
        );

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    private static DocumentCreationController BuildController()
    {
        var documents = new EmptyDocumentRepository();
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            new EmptyVectorStore(),
            Options.Create(new RetrievalOptions { MinScore = 0.0f }),
            NullLogger<RetrievalService>.Instance,
            documents
        );
        var pipeline = new DocumentCreationPipeline(
            retrieval,
            new GenerationService(new FixedLlmClient()),
            documents
        );
        var generator = new DocumentGenerator(new IDocumentRenderer[] { new ThrowingRenderer() });

        return new DocumentCreationController(
            pipeline,
            generator,
            new FixedLlmClient(),
            NullLogger<DocumentCreationController>.Instance
        );
    }

    private sealed class ThrowingRenderer : IDocumentRenderer
    {
        public DocumentOutputFormat Format => DocumentOutputFormat.Txt;

        public GeneratedDocument Render(QueryDetail detail) =>
            throw new InvalidOperationException("renderer exploded");

        public GeneratedDocument Render(DocumentDraft draft) =>
            throw new InvalidOperationException("renderer exploded");
    }

    private sealed class FixedEmbedder : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(_ => new float[384]).ToList()
            );
    }

    private sealed class EmptyVectorStore : IVectorStore
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] queryVector,
            int limit,
            string? documentIdFilter = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());

        public Task UpsertAsync(
            IEnumerable<VectorRecord> vectors,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FixedLlmClient : ILlmClient
    {
        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => Task.FromResult("A welcome note.");

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class EmptyDocumentRepository : IDocumentRepository
    {
        public Task<Document?> FindByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<Document?>(null);

        public Task<DocumentsMinePage> ListMineAsync(
            string sub,
            int? limit,
            int? offset,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Document>> ListAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<Document?> FindByHashAsync(
            string hash,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task AddAsync(Document document, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task AddChunksAsync(
            IEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task DeleteChunksAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Chunk>> GetChunksByIdsAsync(
            IEnumerable<Guid> chunkIds,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<IReadOnlyList<Guid>> ListReadyDocumentIdsWithoutParentChunkAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task UpdateStatusAsync(
            Guid documentId,
            DocumentStatus status,
            string? failureReason = null,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();

        public Task<bool> DeleteAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new NotImplementedException();
    }
}
