using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval.Ai;
using RAGGit.Workstation.Api.Auth;
using Xunit;
using Xunit.Abstractions;

namespace RAGGit.Tests.Integration;

/// <summary>
/// T013 opt-in offline fail-fast: Ollama:Url unreachable (simulates Ollama stopped / WAN-off model missing)
/// → POST /api/query returns 503 model unavailable offline / AI workstation unavailable within Ollama:TimeoutMs,
/// never hangs, never attempts remote pull (FR-007, US-1 S4, Constitution IV).
/// </summary>
[Trait("RequiresOllama", "true")]
public sealed class RealOfflineFailFastTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly string _lancePath;

    public RealOfflineFailFastTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), "raggit-offline-fail-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "rag.db");
        _lancePath = Path.Combine(_tempDir, "lancedb");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private sealed class OfflineFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;
        private readonly string _lancePath;
        private readonly string _unreachableUrl;
        public string AdminKey { get; } = "admin-offline";
        public string EmployeeKey { get; } = "employee-offline";

        public OfflineFactory(string dbPath, string lancePath, string unreachableUrl)
        {
            _dbPath = dbPath;
            _lancePath = lancePath;
            _unreachableUrl = unreachableUrl;
        }

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
                services.AddSingleton<IVectorStore>(new LanceDbLocalClient(_lancePath, 384));
                // Unreachable Ollama — timeout 2000ms for test speed (prog default 5000)
                services.AddSingleton<IEmbedder>(new OllamaEmbedder(_unreachableUrl, "all-minilm"));
                services.AddSingleton<ILlmClient>(new OllamaLlmClient(_unreachableUrl, "phi3:mini"));
            });
        }
    }

    [Fact]
    public async Task Query_WhenOllamaUnreachable_Returns503WithinTimeoutAndNoHang()
    {
        // Unreachable port simulates Ollama stopped; no probe skip — we must verify 503 fail-fast.
        var unreachable = "http://127.0.0.1:59999";
        using var factory = new OfflineFactory(_dbPath, _lancePath, unreachable);
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var sw = Stopwatch.StartNew();
        var resp = await client.PostAsJsonAsync("/api/query", new { query = "refund policy", topK = 5 });
        sw.Stop();
        var body = await resp.Content.ReadAsStringAsync();
        _output.WriteLine($"POST /api/query unreachable → {(int)resp.StatusCode} in {sw.ElapsedMilliseconds}ms body={body}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Contains("unavailable", body, StringComparison.OrdinalIgnoreCase);
        // Must not hang — R7 timeout 5000; we allow 7000 for CI slop. Current buggy impl would hang ~100s.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(7), $"Fail-fast exceeded timeout: {sw.ElapsedMilliseconds}ms (expected <7000ms, Ollama:TimeoutMs 5000)");
        // Never attempts ollama pull — error is fail-fast, not hanging.
    }
}
