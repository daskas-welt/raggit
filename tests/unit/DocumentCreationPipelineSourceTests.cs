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
/// 034 US2: source-backed creation uses retrieved chunks, names the source, and
/// never presents an uncited or unknown chunk as a source.
/// </summary>
public sealed class DocumentCreationPipelineSourceTests
{
    [Fact]
    public async Task Create_WithRelevantChunk_MapsOnlyTheRetrievedSource()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var pipeline = Build(
            documentId,
            chunkId,
            $"Refunds are available within 30 days. [{chunkId}]"
        );

        var draft = await pipeline.CreateAsync(
            new DocumentCreationRequest
            {
                Description = "Create a note about the refund policy.",
                Format = "docx",
            },
            CancellationToken.None
        );

        draft.Sources.Should().ContainSingle();
        draft.Sources[0].DocumentId.Should().Be(documentId);
        draft.Sources[0].ChunkId.Should().Be(chunkId);
        draft.Sources[0].DocumentName.Should().Be("refund-policy.txt");
        draft.Sources[0].Text.Should().Contain("30 days");
    }

    [Fact]
    public async Task Create_WithSourceBackedAnswerButNoCitation_ThrowsSafely()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var pipeline = Build(documentId, chunkId, "Refunds are available within 30 days.");

        var act = () =>
            pipeline.CreateAsync(
                new DocumentCreationRequest
                {
                    Description = "Create a note about the refund policy.",
                    Format = "txt",
                },
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<DocumentCreationException>()
            .Where(exception => exception.Error == DocumentCreationError.NotGroundable);
    }

    [Fact]
    public async Task Create_WithCitationOutsideTheRetrievedSet_ThrowsSafely()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var pipeline = Build(documentId, chunkId, $"Refunds within 30 days. [{Guid.NewGuid()}]");

        var act = () =>
            pipeline.CreateAsync(
                new DocumentCreationRequest
                {
                    Description = "Create a note about the refund policy.",
                    Format = "txt",
                },
                CancellationToken.None
            );

        await act.Should()
            .ThrowAsync<DocumentCreationException>()
            .Where(exception => exception.Error == DocumentCreationError.NotGroundable);
    }

    private static DocumentCreationPipeline Build(Guid documentId, Guid chunkId, string answer)
    {
        var documents = new NamedDocumentRepository(documentId, "refund-policy.txt");
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            new FixedVectorStore(
                new SearchResult(
                    chunkId,
                    documentId.ToString(),
                    "Refunds are available within 30 days.",
                    0,
                    0.9f
                )
            ),
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
        ) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs
                    .Select(_ =>
                    {
                        var vector = new float[384];
                        vector[0] = 1.0f;
                        return vector;
                    })
                    .ToList()
            );
    }

    private sealed class FixedVectorStore(SearchResult result) : IVectorStore
    {
        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] queryVector,
            int limit,
            string? documentIdFilter = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<SearchResult>>(new[] { result });

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

    private sealed class NamedDocumentRepository(Guid id, string filename) : IDocumentRepository
    {
        public Task<Document?> FindByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<Document?>(
                documentId == id ? new Document { Id = id, Filename = filename } : null
            );

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
