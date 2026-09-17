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
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class CoverageBoostTests
{
    [Fact]
    public void CorruptDocumentException_Message()
    {
        var ex = new CorruptDocumentException("corrupted pdf");
        ex.Message.Should().Be("corrupted pdf");
        var ex2 = new CorruptDocumentException("corrupted docx", new InvalidOperationException());
        ex2.InnerException.Should().NotBeNull();
    }

    [Fact]
    public async Task DocumentFormatValidator_AllMimes()
    {
        using var pdf = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var r1 = await DocumentFormatValidator.ValidateAndRewindAsync(pdf, DocumentMimeType.Pdf);
        r1.Position.Should().Be(0);

        using var docx = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00 });
        var r2 = await DocumentFormatValidator.ValidateAndRewindAsync(docx, DocumentMimeType.Docx);
        r2.Position.Should().Be(0);

        using var txt = new MemoryStream(Encoding.UTF8.GetBytes("plain text"));
        var r3 = await DocumentFormatValidator.ValidateAndRewindAsync(txt, DocumentMimeType.Txt);
        r3.Position.Should().Be(0);
    }

    [Fact]
    public void Chunker_ChunkText_Basic()
    {
        var chunks = Chunker.ChunkText(
            "hello world this is a test of chunking",
            Guid.NewGuid(),
            4,
            1
        );
        chunks.Count.Should().BeGreaterThan(0);
        chunks[0].TokenCount.Should().Be(4);
    }

    [Fact]
    public async Task IngestService_DuplicateHash_NoReindex()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "raggit-cov-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var dbPath = Path.Combine(tmp, "rag.db");
        var db = new RagDbContext($"Data Source={dbPath}");
        await db.EnsureCreatedAsync();
        var store = new FakeVectorStoreForCov();
        var embedder = new FakeEmbedderForCov();
        var svc = new IngestService(
            embedder,
            store,
            db,
            NullLogger<IngestService>.Instance,
            contentStore: new FileDocumentContentStore(Path.Combine(tmp, "originals")),
            options: Options.Create(
                new IngestOptions
                {
                    ChunkSize = 50,
                    ChunkOverlap = 10,
                    EmbedBatchSize = 2,
                }
            )
        );

        var content = "duplicate content for cov";
        var bytes = Encoding.UTF8.GetBytes(content);
        var (doc1, created1) = await svc.IngestAsync(
            new MemoryStream(bytes),
            "a.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        created1.Should().BeTrue();
        var (doc2, created2) = await svc.IngestAsync(
            new MemoryStream(bytes),
            "b.txt",
            DocumentMimeType.Txt,
            bytes.Length,
            "tester"
        );
        created2.Should().BeFalse();
        doc2.Id.Should().Be(doc1.Id);
        try
        {
            Directory.Delete(tmp, true);
        }
        catch { }
    }

    [Fact]
    public async Task IngestService_CorruptPdf_RollsBack()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "raggit-cov2-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var dbPath = Path.Combine(tmp, "rag.db");
        var db = new RagDbContext($"Data Source={dbPath}");
        await db.EnsureCreatedAsync();
        var store = new FakeVectorStoreForCov();
        var embedder = new FakeEmbedderForCov();
        var svc = new IngestService(
            embedder,
            store,
            db,
            NullLogger<IngestService>.Instance,
            contentStore: new FileDocumentContentStore(Path.Combine(tmp, "originals"))
        );

        // Docx with PK header but invalid zip -> OpenXml throws -> CorruptDocumentException via Chunker
        var badDocx = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF, 0x00, 0x00, 0x01, 0x02 };
        var act = async () =>
            await svc.IngestAsync(
                new MemoryStream(badDocx),
                "bad.docx",
                DocumentMimeType.Docx,
                badDocx.Length,
                "tester"
            );
        await act.Should().ThrowAsync<CorruptDocumentException>();

        var docs = await svc.ListDocumentsAsync();
        docs.Should().NotContain(d => d.Filename == "bad.docx");
        try
        {
            Directory.Delete(tmp, true);
        }
        catch { }
    }

    [Fact]
    public async Task LanceDbLocalClient_ValidateDimension_NoTable_NoThrow()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "raggit-cov-ldb-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var client = new LanceDbLocalClient(tmp, 384);
        await client.ValidateDimensionAsync(384);
        await client.ValidateDimensionAsync(768);
        Directory.Delete(tmp, true);
    }

    [Fact]
    public async Task RetrievalService_Basic()
    {
        var store = new FakeVectorStoreForCov();
        var embedder = new FakeEmbedderForCov();
        var retrieval = new RetrievalService(embedder, store);
        var result = await retrieval.RetrieveAsync("hello", 5);
        result.Should().NotBeNull();
    }

    private sealed class FakeVectorStoreForCov : IVectorStore
    {
        public Task UpsertAsync(
            IEnumerable<VectorRecord> vectors,
            CancellationToken ct = default
        ) => Task.CompletedTask;

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] q,
            int limit,
            string? f = null,
            CancellationToken ct = default
        ) => Task.FromResult<IReadOnlyList<SearchResult>>(new List<SearchResult>());

        public Task DeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeEmbedderForCov : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
            IEnumerable<string> inputs,
            CancellationToken ct = default
        )
        {
            var list = new List<float[]>();
            foreach (var _ in inputs)
            {
                var v = new float[384];
                v[0] = 1.0f;
                list.Add(v);
            }
            return Task.FromResult<IReadOnlyList<float[]>>(list);
        }
    }

    private sealed class FakeLlmForCov : ILlmClient
    {
        public Task<string> ChatAsync(string s, string u, CancellationToken ct = default) =>
            Task.FromResult("answer");

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
