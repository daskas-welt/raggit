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
/// 032 T008: the one pipeline both transports run. It reports the work at the
/// moment it happens, never claims sources it did not find, and returns the same
/// outcome the classic controller path used to build.
/// </summary>
public sealed class QueryPipelineTests
{
    [Fact]
    public async Task Execute_WithSources_ReportsTheFourStagesInOrder()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var pipeline = Build(
            chunks:
            [
                new SearchResult(
                    chunkId,
                    documentId.ToString(),
                    "The renewal term is 30 days.",
                    0,
                    0.9f
                ),
            ],
            documentName: "policy.pdf",
            answer: $"The renewal term is 30 days. [{chunkId}]"
        );

        var progress = await RunAsync(pipeline, "What is the renewal term?");

        progress
            .Select(p => p.Stage)
            .Should()
            .Equal(
                QueryStage.Understanding,
                QueryStage.Searching,
                QueryStage.Sources,
                QueryStage.Composing
            );
    }

    [Fact]
    public async Task Execute_SourcesStage_CarriesTheCountAndNames()
    {
        var documentId = Guid.NewGuid();
        var pipeline = Build(
            chunks:
            [
                new SearchResult(Guid.NewGuid(), documentId.ToString(), "first", 0, 0.9f),
                new SearchResult(Guid.NewGuid(), documentId.ToString(), "second", 1, 0.8f),
            ],
            documentName: "policy.pdf",
            answer: "an answer"
        );

        var progress = await RunAsync(pipeline, "What is the renewal term?");

        var sources = progress.Single(p => p.Stage == QueryStage.Sources);
        // Two chunks of one document are one source, and the name is the one the
        // answer's own citations will show.
        sources.SourceCount.Should().Be(1);
        sources.DocumentNames.Should().Equal("policy.pdf");
    }

    [Fact]
    public async Task Execute_SourceFields_AppearOnlyOnTheSourcesStage()
    {
        var documentId = Guid.NewGuid();
        var pipeline = Build(
            chunks: [new SearchResult(Guid.NewGuid(), documentId.ToString(), "text", 0, 0.9f)],
            documentName: "policy.pdf",
            answer: "an answer"
        );

        var progress = await RunAsync(pipeline, "question");

        progress
            .Where(p => p.Stage != QueryStage.Sources)
            .Should()
            .OnlyContain(p => p.SourceCount == null && p.DocumentNames == null);
    }

    [Fact]
    public async Task Execute_WithoutRelevantChunks_NeverReportsSources()
    {
        var pipeline = Build(chunks: [], documentName: null, answer: "unused");

        var progress = await RunAsync(pipeline, "nothing in the library matches this");

        progress.Select(p => p.Stage).Should().NotContain(QueryStage.Sources);
        progress.Select(p => p.Stage).Should().NotContain(QueryStage.Composing);
    }

    [Fact]
    public async Task Execute_WithoutRelevantChunks_ReturnsExactlyTheRefusal()
    {
        var pipeline = Build(chunks: [], documentName: null, answer: "unused");

        var outcome = await pipeline.ExecuteAsync(new QueryRequest { Query = "no match" });

        outcome.Answer.Should().Be("no relevant content found");
        outcome.Citations.Should().BeEmpty();
        outcome.RetrievedChunkIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_WithoutAnObserver_StillReturnsTheFullOutcome()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var pipeline = Build(
            chunks: [new SearchResult(chunkId, documentId.ToString(), "text", 0, 0.9f)],
            documentName: "policy.pdf",
            answer: $"answered. [{chunkId}]"
        );

        var outcome = await pipeline.ExecuteAsync(new QueryRequest { Query = "q" });

        outcome.Answer.Should().Be($"answered. [{chunkId}]");
        outcome.Citations.Should().ContainSingle().Which.ChunkId.Should().Be(chunkId);
        outcome.RetrievedChunkIds.Should().Equal(chunkId);
        outcome.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Execute_UnknownDocumentName_OmitsTheNameWithoutLeakingTheId()
    {
        var documentId = Guid.NewGuid();
        var pipeline = Build(
            chunks: [new SearchResult(Guid.NewGuid(), documentId.ToString(), "text", 0, 0.9f)],
            documentName: null,
            answer: "an answer"
        );

        var progress = await RunAsync(pipeline, "question");

        var sources = progress.Single(p => p.Stage == QueryStage.Sources);
        sources.SourceCount.Should().Be(1);
        sources.DocumentNames.Should().BeEmpty();
        sources.Text.Should().NotContain(documentId.ToString());
    }

    [Fact]
    public async Task Execute_WithoutTopK_UsesTheConfiguredRetrievalDepth()
    {
        var documentId = Guid.NewGuid();
        var chunks = Enumerable
            .Range(0, 5)
            .Select(i => new SearchResult(
                Guid.NewGuid(),
                documentId.ToString(),
                $"chunk {i}",
                i,
                0.9f - (i * 0.01f)
            ))
            .ToList();
        var pipeline = Build(
            chunks,
            "policy.pdf",
            "an answer",
            new RetrievalOptions { MinScore = 0.0f, TopK = 2 }
        );

        var outcome = await pipeline.ExecuteAsync(new QueryRequest { Query = "question" });

        outcome.RetrievedChunkIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_WithTopK_OverridesTheConfiguredDepth()
    {
        var documentId = Guid.NewGuid();
        var chunks = Enumerable
            .Range(0, 5)
            .Select(i => new SearchResult(
                Guid.NewGuid(),
                documentId.ToString(),
                $"chunk {i}",
                i,
                0.9f - (i * 0.01f)
            ))
            .ToList();
        var pipeline = Build(
            chunks,
            "policy.pdf",
            "an answer",
            new RetrievalOptions { MinScore = 0.0f, TopK = 2 }
        );

        var outcome = await pipeline.ExecuteAsync(
            new QueryRequest { Query = "question", TopK = 4 }
        );

        outcome.RetrievedChunkIds.Should().HaveCount(4);
    }

    private static async Task<List<QueryProgress>> RunAsync(QueryPipeline pipeline, string query)
    {
        var progress = new List<QueryProgress>();
        await pipeline.ExecuteAsync(
            new QueryRequest { Query = query },
            (update, _) =>
            {
                progress.Add(update);
                return ValueTask.CompletedTask;
            }
        );

        return progress;
    }

    private static QueryPipeline Build(
        IReadOnlyList<SearchResult> chunks,
        string? documentName,
        string answer,
        RetrievalOptions? options = null
    )
    {
        var documents = new NamingDocumentRepository(documentName);
        var retrieval = new RetrievalService(
            new FixedEmbedder(),
            new FixedVectorStore(chunks),
            Options.Create(options ?? new RetrievalOptions { MinScore = 0.0f }),
            NullLogger<RetrievalService>.Instance,
            documents
        );

        return new QueryPipeline(
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

    private sealed class FixedVectorStore : IVectorStore
    {
        private readonly IReadOnlyList<SearchResult> _results;

        public FixedVectorStore(IReadOnlyList<SearchResult> results) => _results = results;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] queryVector,
            int limit,
            string? documentIdFilter = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(_results);

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
        private readonly string _answer;

        public FixedLlmClient(string answer) => _answer = answer;

        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(_answer);

        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    /// <summary>
    /// Resolves every document id to one configured filename, or to nothing when
    /// no name is configured (the "unknown filename" case).
    /// </summary>
    private sealed class NamingDocumentRepository : IDocumentRepository
    {
        private readonly string? _filename;

        public NamingDocumentRepository(string? filename) => _filename = filename;

        public Task<Document?> FindByIdAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult<Document?>(
                _filename is null ? null : new Document { Id = documentId, Filename = _filename }
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
