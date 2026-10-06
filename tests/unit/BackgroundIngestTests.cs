using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using RAGGit.Workstation.Api;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Unit tests for the background-ingest contract: staging a document reports it
/// as <see cref="DocumentStatus.Uploading"/> (the state the client polls on),
/// failures are retryable by re-uploading, and a failure records a short,
/// user-safe reason instead of leaking internals.
/// </summary>
public sealed class BackgroundIngestTests
{
    [Fact]
    public async Task StageAsync_NewDocument_IsUploading_AndNotIndexedYet()
    {
        using var fixture = new IngestFixture();
        var bytes = Encoding.UTF8.GetBytes("staged content for the background worker");

        var (staged, created) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "staged.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );

        created.Should().BeTrue();
        staged.Status.Should().Be(DocumentStatus.Uploading);
        staged.FailureReason.Should().BeNull();

        var reloaded = await fixture.Repository.FindByIdAsync(staged.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Status.Should().Be(DocumentStatus.Uploading);
    }

    [Fact]
    public async Task StageAsync_DuplicateReadyDocument_IsReturnedUntouched()
    {
        using var fixture = new IngestFixture();
        var bytes = Encoding.UTF8.GetBytes("already indexed content");

        var (first, _) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "ready.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        await fixture.Repository.UpdateStatusAsync(first.Id, DocumentStatus.Ready);

        var (second, created) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "ready.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );

        created.Should().BeFalse();
        second.Id.Should().Be(first.Id);
        second.Status.Should().Be(DocumentStatus.Ready);
    }

    [Fact]
    public async Task StageAsync_FailedDuplicate_IsRestagedSoRetryIsPossible()
    {
        using var fixture = new IngestFixture();
        var bytes = Encoding.UTF8.GetBytes("retryable content");

        var (first, _) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "retry.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        await fixture.Repository.UpdateStatusAsync(
            first.Id,
            DocumentStatus.Failed,
            "corrupted txt"
        );

        var (retried, created) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "retry.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );

        created.Should().BeTrue("a failed document must be handed back to the work queue");
        retried.Id.Should().Be(first.Id);
        retried.Status.Should().Be(DocumentStatus.Uploading);
        retried.FailureReason.Should().BeNull();

        var reloaded = await fixture.Repository.FindByIdAsync(first.Id);
        reloaded!.Status.Should().Be(DocumentStatus.Uploading);
        reloaded.FailureReason.Should().BeNull("the stale reason must not survive a retry");
    }

    [Fact]
    public async Task ProcessStagedAsync_PermanentFailure_RecordsReasonAndStatus()
    {
        using var fixture = new IngestFixture();

        // PK header with an invalid zip body: staging accepts it (no extraction),
        // the background worker rejects it as a corrupt docx.
        var badDocx = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF, 0x00, 0x00, 0x01, 0x02 };
        var (staged, created) = await fixture.Service.StageAsync(
            new MemoryStream(badDocx),
            "bad.docx",
            DocumentMimeType.Docx,
            badDocx.Length,
            "tester"
        );
        created.Should().BeTrue();

        await fixture.Service.ProcessStagedAsync(staged.Id);

        var reloaded = await fixture.Repository.FindByIdAsync(staged.Id);
        reloaded!.Status.Should().Be(DocumentStatus.Failed);
        reloaded.FailureReason.Should().Be("corrupted docx");
    }

    [Fact]
    public async Task ProcessStagedAsync_UnexpectedFailure_UsesGenericReasonWithoutInternals()
    {
        using var fixture = new IngestFixture(contentStore: new ThrowingContentStore());
        var bytes = Encoding.UTF8.GetBytes("unreadable original");

        var (staged, _) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "unreadable.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );

        await fixture.Service.ProcessStagedAsync(staged.Id);

        var reloaded = await fixture.Repository.FindByIdAsync(staged.Id);
        reloaded!.Status.Should().Be(DocumentStatus.Failed);
        reloaded.FailureReason.Should().NotBeNullOrWhiteSpace();
        reloaded.FailureReason.Should().NotContain("boom");
        reloaded.FailureReason.Should().NotContain("Exception");
    }

    [Fact]
    public async Task EnqueueAsync_MarksDocumentQueued_AndPublishesIt()
    {
        using var fixture = new IngestFixture();
        var bytes = Encoding.UTF8.GetBytes("queued content");
        var (staged, _) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "queued.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        var queue = new IngestWorkQueue(fixture.Service, NullLogger<IngestWorkQueue>.Instance);

        await queue.EnqueueAsync(staged.Id);

        // Queued is produced at the hand-off to the worker, not by staging.
        var reloaded = await fixture.Repository.FindByIdAsync(staged.Id);
        reloaded!.Status.Should().Be(DocumentStatus.Queued);
        reloaded.FailureReason.Should().BeNull();
        (await ReadPublishedAsync(queue)).Should().ContainSingle().Which.Should().Be(staged.Id);
    }

    [Fact]
    public async Task Queue_TracksOutstandingAndDirty_UntilProcessed()
    {
        using var fixture = new IngestFixture();
        var bytes = Encoding.UTF8.GetBytes("tracked content");
        var (staged, _) = await fixture.Service.StageAsync(
            new MemoryStream(bytes),
            "tracked.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        var queue = new IngestWorkQueue(fixture.Service, NullLogger<IngestWorkQueue>.Instance);

        queue.OutstandingCount.Should().Be(0);
        queue.ConsumeDirty().Should().BeFalse("nothing has been ingested yet");

        await queue.EnqueueAsync(staged.Id);

        queue.OutstandingCount.Should().Be(1);
        queue.ConsumeDirty().Should().BeTrue();
        queue.ConsumeDirty().Should().BeFalse("the dirty flag is consumed once");

        queue.MarkProcessed();
        queue.OutstandingCount.Should().Be(0);

        // A failed maintenance pass re-arms the flag so the next drained tick retries.
        queue.MarkDirty();
        queue.ConsumeDirty().Should().BeTrue();
    }

    private static async Task<IReadOnlyList<Guid>> ReadPublishedAsync(IngestWorkQueue queue)
    {
        var published = new List<Guid>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await foreach (var documentId in queue.ReadAllAsync(cancellation.Token))
            {
                published.Add(documentId);
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing published within the timeout; the assertion reports it.
        }

        return published;
    }

    /// <summary>
    /// Per-test temp SQLite database, stored originals, and an
    /// <see cref="IngestService"/> wired to deterministic fakes.
    /// </summary>
    private sealed class IngestFixture : IDisposable
    {
        private readonly string _root;

        public IngestFixture(IDocumentContentStore? contentStore = null)
        {
            _root = Path.Combine(Path.GetTempPath(), "raggit-bg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Db = new RagDbContext($"Data Source={Path.Combine(_root, "rag.db")}");
            Db.EnsureCreatedAsync().GetAwaiter().GetResult();
            Repository = new SqliteDocumentRepository(Db);
            Service = new IngestService(
                new FakeEmbedder(),
                new FakeVectorStore(),
                Repository,
                NullLogger<IngestService>.Instance,
                contentStore: contentStore
                    ?? new FileDocumentContentStore(Path.Combine(_root, "originals")),
                options: Options.Create(
                    new IngestOptions
                    {
                        ChunkSize = 50,
                        ChunkOverlap = 10,
                        EmbedBatchSize = 2,
                    }
                )
            );
        }

        public RagDbContext Db { get; }

        public SqliteDocumentRepository Repository { get; }

        public IngestService Service { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch
            {
                // Temp cleanup is best-effort; a locked SQLite file must not fail the test.
            }
        }
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public Task UpsertAsync(
            IEnumerable<VectorRecord> vectors,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] query,
            int limit,
            string? filter = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<SearchResult>>(new List<SearchResult>());

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeEmbedder : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken ct = default
        )
        {
            var embeddings = new List<float[]>();
            foreach (var _ in inputs)
            {
                var vector = new float[384];
                vector[0] = 1.0f;
                embeddings.Add(vector);
            }

            return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
        }
    }

    /// <summary>
    /// Simulates an unexpected storage fault whose message must never reach the
    /// client verbatim.
    /// </summary>
    private sealed class ThrowingContentStore : IDocumentContentStore
    {
        public Task SaveAsync(
            Guid documentId,
            Stream content,
            CancellationToken cancellationToken = default
        ) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(
            Guid documentId,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("boom: internal storage detail");

        public Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
