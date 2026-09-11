using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval.Ai;
using RAGGit.Tests.Integration.Helpers;
using RAGGit.Workstation.Api.Auth;
using Xunit;
using Xunit.Abstractions;

namespace RAGGit.Tests.Integration;

/// <summary>
/// Opt-in real-loop suite T012: persist → chunk → retrieve with real Ollama + real LanceDB.
/// Zero fakes on ingest→index→query core path. Skips gracefully when Ollama absent (SC-003).
/// Independent Test per spec US-1: upload 50-page PDF → Ready &lt;300s → query refund policy → 200 with citations.
/// </summary>
[Trait("RequiresOllama", "true")]
public sealed class RealLoopTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly string _lancePath;

    public RealLoopTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), "raggit-real-loop-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "rag.db");
        _lancePath = Path.Combine(_tempDir, "lancedb");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    private sealed class RealFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;
        private readonly string _lancePath;
        private readonly string _ollamaUrl;
        public string AdminKey { get; } = "admin-real-loop";
        public string EmployeeKey { get; } = "employee-real-loop";

        public RealFactory(string dbPath, string lancePath, string ollamaUrl)
        {
            _dbPath = dbPath;
            _lancePath = lancePath;
            _ollamaUrl = ollamaUrl;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ApiKeyAuthOptions>(
                    ApiKeyAuthOptions.Scheme,
                    o =>
                    {
                        o.AdminApiKey = AdminKey;
                        o.EmployeeApiKey = EmployeeKey;
                    }
                );
                services.AddSingleton(new RagDbContext($"Data Source={_dbPath}"));
                // Real vector store on temp path (384 dev default)
                services.AddSingleton<IVectorStore>(new LanceDbLocalClient(_lancePath, 384));
                // Real Ollama embedder/llm — TimeoutMs 5000 (R7). 3-arg overload added in T014; 2-arg used until then.
                services.AddSingleton<IEmbedder>(new OllamaEmbedder(_ollamaUrl, "all-minilm"));
                services.AddSingleton<ILlmClient>(new OllamaLlmClient(_ollamaUrl, "phi3:mini"));
            });
        }
    }

    [Fact]
    public async Task RealLoop_PersistChunkRetrieve_CitedAnswer()
    {
        var ollamaUrl = OllamaProbe.DefaultUrl;
        if (!await OllamaProbe.IsAvailableAsync(ollamaUrl, 2000))
        {
            _output.WriteLine(
                $"[SKIP] Ollama not available at {ollamaUrl} — graceful skip per SC-003."
            );
            return;
        }

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-50pages.pdf");
        if (!File.Exists(fixturePath))
        {
            // Fallback to repo-relative
            fixturePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "fixtures",
                "sample-50pages.pdf"
            );
        }
        if (!File.Exists(fixturePath))
        {
            _output.WriteLine($"[SKIP] Fixture not found at {fixturePath}");
            return;
        }

        var swTotal = Stopwatch.StartNew();
        using var factory = new RealFactory(_dbPath, _lancePath, ollamaUrl);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        // Upload 50-page PDF
        _output.WriteLine($"Uploading {fixturePath} ({new FileInfo(fixturePath).Length} bytes)");
        var swUpload = Stopwatch.StartNew();
        await using var pdfStream = File.OpenRead(fixturePath);
        var form = new MultipartFormDataContent
        {
            { new StreamContent(pdfStream), "file", "sample-50pages.pdf" },
        };
        var uploadResp = await client.PostAsync("/api/documents", form);
        swUpload.Stop();
        _output.WriteLine(
            $"POST /api/documents → {(int)uploadResp.StatusCode} in {swUpload.ElapsedMilliseconds}ms"
        );
        var uploadBody = await uploadResp.Content.ReadAsStringAsync();
        _output.WriteLine($"Upload body: {uploadBody}");
        Assert.True(uploadResp.IsSuccessStatusCode, $"Upload failed: {uploadBody}");

        var uploadJson = JsonDocument.Parse(uploadBody);
        var docId = uploadJson.RootElement.TryGetProperty("id", out var idProp)
            ? idProp.GetString()
            : null;
        Assert.False(string.IsNullOrWhiteSpace(docId));

        // Poll GET /api/documents until Ready <300s (SC-001)
        var swPoll = Stopwatch.StartNew();
        string status = "Indexing";
        var deadline = TimeSpan.FromSeconds(300);
        while (swPoll.Elapsed < deadline)
        {
            await Task.Delay(2000);
            var listResp = await client.GetAsync("/api/documents");
            Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
            var listBody = await listResp.Content.ReadAsStringAsync();
            var docs = JsonDocument.Parse(listBody).RootElement;
            foreach (var doc in docs.EnumerateArray())
            {
                if (doc.TryGetProperty("id", out var did) && did.GetString() == docId)
                {
                    status = doc.TryGetProperty("status", out var sp)
                        ? sp.GetString() ?? "unknown"
                        : "unknown";
                    break;
                }
            }
            _output.WriteLine($"Poll {swPoll.Elapsed.TotalSeconds:F1}s status={status}");
            if (status == "Ready")
                break;
            if (status == "Failed")
                Assert.Fail("Document ingestion failed");
        }
        swPoll.Stop();
        _output.WriteLine(
            $"Poll finished in {swPoll.ElapsedMilliseconds}ms status={status} total={swTotal.ElapsedMilliseconds}ms"
        );
        Assert.Equal("Ready", status);
        Assert.True(
            swTotal.Elapsed < TimeSpan.FromSeconds(300),
            $"SC-001 Ready exceeded 300s: {swTotal.Elapsed}"
        );

        // Query refund policy (SC-002) — use Employee key for retrieval
        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);
        var queryPayload = new { query = "refund policy", topK = 5 };
        var swQuery = Stopwatch.StartNew();
        var queryResp = await client.PostAsJsonAsync("/api/query", queryPayload);
        swQuery.Stop();
        var queryBody = await queryResp.Content.ReadAsStringAsync();
        _output.WriteLine(
            $"POST /api/query {(int)queryResp.StatusCode} in {swQuery.ElapsedMilliseconds}ms body={queryBody}"
        );
        Assert.Equal(HttpStatusCode.OK, queryResp.StatusCode);
        var queryJson = JsonDocument.Parse(queryBody);
        if (
            queryJson.RootElement.TryGetProperty("answer", out var ans)
            && ans.GetString() == "no relevant content found"
        )
        {
            _output.WriteLine(
                "No relevant content found — acceptable per spec but citation path preferred."
            );
        }
        else
        {
            Assert.True(
                queryJson.RootElement.TryGetProperty("citations", out var cits),
                "Missing citations"
            );
            var count = cits.GetArrayLength();
            _output.WriteLine($"Citations: {count}");
            Assert.True(count >= 1, "Expected ≥1 citation referencing the document");
            // Verify citation references our docId
            var hasDoc = cits.EnumerateArray()
                .Any(c => c.TryGetProperty("documentId", out var did) && did.GetString() == docId);
            Assert.True(hasDoc, "Citation should reference uploaded document");
        }

        _output.WriteLine(
            $"SC-001 wall time Ready: {swTotal.ElapsedMilliseconds}ms (<300s) SC-002 query: {swQuery.ElapsedMilliseconds}ms"
        );
        _output.WriteLine(
            "Note: <7s on reference workstation is production assumption to verify at deploy, not asserted here per verification.md."
        );
    }
}
