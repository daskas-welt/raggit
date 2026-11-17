using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 034 US1: an empty library still yields a document draft, and the creation
/// path has no query-history write dependency.
/// </summary>
public sealed class DocumentCreationPipelineTests
{
    [Fact]
    public async Task Create_WithEmptyLibrary_ReturnsSourceFreeDraft()
    {
        var pipeline = Build(answer: "Welcome to the team.");

        var draft = await pipeline.CreateAsync(
            new DocumentCreationRequest
            {
                Description = "Draft a welcome note for a new team member.",
                Format = "docx",
            },
            CancellationToken.None
        );

        draft.Description.Should().Be("Draft a welcome note for a new team member.");
        draft.Body.Should().Be("Welcome to the team.");
        draft.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_DoesNotExposeAQueryHistoryWriteDependency()
    {
        var pipeline = Build(answer: "A note.");

        var constructorParameters = pipeline
            .GetType()
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        constructorParameters.Should().NotContain(typeof(IQueryRepository));
    }

    private static DocumentCreationPipeline Build(string answer)
    {
        var documents = new EmptyDocumentRepository();
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            new EmptyVectorStore(),
            Options.Create(new RetrievalOptions { MinScore = 0.0f }),
            NullLogger<RetrievalService>.Instance,
            documents
        );

        return new DocumentCreationPipeline(
            retrieval,
            new GenerationService(new FixedLlmClient(answer)),
            documents
        );
    }

    private sealed class FixedEmbedder : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken cancellationToken = default
        )
        {
            var vectors = inputs
                .Select(_ =>
                {
                    var vector = new float[384];
                    vector[0] = 1.0f;
                    return vector;
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }
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

    private sealed class FixedLlmClient(string answer) : ILlmClient
    {
        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(answer);

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
