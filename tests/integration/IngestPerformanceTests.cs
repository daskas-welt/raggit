using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Workstation.Api.Auth;
using Xunit;
using Xunit.Abstractions;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T016 performance measurement for SC-001/SC-002: 50-page PDF ingest → Ready must be &lt;300s (5 min)
/// and query cited answer measured. On fakes the ingest is &lt;4s; on real Ollama it is &lt;300s.
/// Logs wall-clock for verification.md. Never requires Ollama to pass CI; real timing measured in RealLoopTests.
/// </summary>
public sealed class IngestPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly string _dbPath;

    public IngestPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), "raggit-perf-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "rag.db");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private sealed class PerfFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;
        public string AdminKey { get; } = "admin-perf";
        public string EmployeeKey { get; } = "employee-perf";

        public PerfFactory(string dbPath) => _dbPath = dbPath;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ApiKeyAuthOptions>(ApiKeyAuthOptions.Scheme, o =>
                {
                    o.AdminApiKey = AdminKey;
                    o.EmployeeApiKey = EmployeeKey;
                });
                services.AddSingleton(new RagDbContext($"Data Source={_dbPath}"));
                services.AddSingleton<IVectorStore>(new FakeVectorStorePerf());
                services.AddSingleton<IEmbedder>(new FakeEmbedderPerf());
                services.AddSingleton<ILlmClient>(new FakeLlmClientPerf());
            });
        }
    }

    private sealed class FakeVectorStorePerf : IVectorStore
    {
        private readonly List<VectorRecord> _records = new();
        public Task UpsertAsync(IEnumerable<VectorRecord> vectors, CancellationToken ct = default) { lock(_records){_records.AddRange(vectors);} return Task.CompletedTask; }
        public Task<IReadOnlyList<SearchResult>> SearchAsync(float[] q, int limit, string? f=null, CancellationToken ct=default){ lock(_records){ var r=_records.Take(limit).Select(v=> new SearchResult(v.Id, v.Payload.TryGetValue("documentId",out var d)? d?.ToString()??"" : "", v.Payload.TryGetValue("text",out var t)? t?.ToString()??"" : "", 0,1f)).ToList(); return Task.FromResult<IReadOnlyList<SearchResult>>(r);} }
        public Task DeleteAsync(string id, CancellationToken ct=default){ lock(_records){_records.RemoveAll(r=> r.Payload.TryGetValue("documentId",out var d)&& d?.ToString()==id);} return Task.CompletedTask; }
        public Task<bool> IsHealthyAsync(CancellationToken ct=default)=> Task.FromResult(true);
    }
    private sealed class FakeEmbedderPerf : IEmbedder
    {
        public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(IEnumerable<string> inputs, CancellationToken ct=default){ var e=inputs.Select(_=>{var v=new float[384]; v[0]=1f; return v;}).ToList(); return Task.FromResult<IReadOnlyList<float[]>>(e); }
    }
    private sealed class FakeLlmClientPerf : ILlmClient
    {
        public Task<string> ChatAsync(string s, string u, CancellationToken ct=default)=> Task.FromResult("fake answer [00000000-0000-0000-0000-000000000000]");
        public Task<bool> IsHealthyAsync(CancellationToken ct=default)=> Task.FromResult(true);
    }

    [Fact]
    public async Task Ingest_50PagePdf_MustBeFast_SC001()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-50pages.pdf");
        if (!File.Exists(fixturePath))
            fixturePath = Path.Combine(Directory.GetCurrentDirectory(), "fixtures", "sample-50pages.pdf");
        if (!File.Exists(fixturePath))
        {
            _output.WriteLine($"[SKIP] Fixture not found at {fixturePath}");
            return;
        }

        using var factory = new PerfFactory(_dbPath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var sw = Stopwatch.StartNew();
        await using var pdf = File.OpenRead(fixturePath);
        var form = new MultipartFormDataContent
        {
            { new StreamContent(pdf), "file", "sample-50pages.pdf" }
        };
        var resp = await client.PostAsync("/api/documents", form);
        var body = await resp.Content.ReadAsStringAsync();
        _output.WriteLine($"POST /api/documents {(int)resp.StatusCode} body={body} elapsed={sw.ElapsedMilliseconds}ms");
        Assert.True(resp.IsSuccessStatusCode, body);

        // Poll for Ready — with fakes this is instant (<2s); SC-001 budget is 300s.
        var pollSw = Stopwatch.StartNew();
        string status = "";
        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString();
        while (pollSw.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(200);
            var listResp = await client.GetAsync("/api/documents");
            var listBody = await listResp.Content.ReadAsStringAsync();
            var docs = JsonDocument.Parse(listBody).RootElement;
            foreach (var doc in docs.EnumerateArray())
            {
                if (doc.GetProperty("id").GetString() == id)
                {
                    status = doc.GetProperty("status").GetString() ?? "";
                    break;
                }
            }
            if (status == "Ready") break;
        }
        pollSw.Stop();
        sw.Stop();
        _output.WriteLine($"Ingest Ready poll {pollSw.ElapsedMilliseconds}ms total {sw.ElapsedMilliseconds}ms status={status}");
        // SC-001: <300s on dev laptop; with fakes we assert <4s to catch regressions per T016.
        Assert.Equal("Ready", status);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(300), $"SC-001 exceeded 300s: {sw.Elapsed}");
        // Fast-path regression gate: fakes should be <4000ms for 50 pages (chunk 512/50 + fake embed)
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4), $"Performance regression: fake ingest {sw.ElapsedMilliseconds}ms exceeds 4000ms gate");

        // Query performance gate: with fakes, topK=5 should be <2s
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        var qSw = Stopwatch.StartNew();
        var qResp = await client.PostAsJsonAsync("/api/query", new { query = "refund policy", topK = 5 });
        qSw.Stop();
        var qBody = await qResp.Content.ReadAsStringAsync();
        _output.WriteLine($"POST /api/query {(int)qResp.StatusCode} in {qSw.ElapsedMilliseconds}ms body={qBody}");
        Assert.True(qSw.Elapsed < TimeSpan.FromSeconds(2), $"Query exceeded 2s fake gate: {qSw.ElapsedMilliseconds}ms");
        _output.WriteLine("Note: Real SC-002 <7s on reference workstation is production assumption, not asserted on fake path.");
    }
}
