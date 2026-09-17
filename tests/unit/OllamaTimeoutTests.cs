using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Ingest.Ai;
using RAGGit.Retrieval.Ai;
using Xunit;
using Xunit.Abstractions;

namespace RAGGit.Tests.Unit;

/// <summary>
/// T014 unit timeout wiring: OllamaEmbedder/OllamaLlmClient constructed with timeoutMs
/// must fail fast against unreachable host within configured window (R7).
/// No live Ollama needed; unreachable port simulates WAN-off.
/// Must FAIL before T015 (timeout wiring) then turn green.
/// </summary>
public sealed class OllamaTimeoutTests
{
    private readonly ITestOutputHelper _output;

    public OllamaTimeoutTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task OllamaEmbedder_Unreachable_FailsFastWithinTimeout()
    {
        var unreachable = "http://127.0.0.1:59998";
        var timeoutMs = 1500;
        var embedder = new OllamaEmbedder(unreachable, "all-minilm", timeoutMs);
        var sw = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            embedder.GetEmbeddingsAsync(new[] { "hello" })
        );
        sw.Stop();
        _output.WriteLine(
            $"Embedder threw {ex.GetType().Name}: {ex.Message} in {sw.ElapsedMilliseconds}ms"
        );
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Embedder fail-fast exceeded {timeoutMs}ms window: {sw.ElapsedMilliseconds}ms"
        );
        Assert.True(
            sw.Elapsed < TimeSpan.FromMilliseconds(timeoutMs + 1500),
            $"Expected ~{timeoutMs}ms, got {sw.ElapsedMilliseconds}ms"
        );
    }

    [Fact]
    public async Task OllamaLlmClient_Unreachable_FailsFastWithinTimeout()
    {
        var unreachable = "http://127.0.0.1:59997";
        var timeoutMs = 1500;
        var llm = new OllamaLlmClient(unreachable, "phi3:mini", timeoutMs);
        var sw = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => llm.ChatAsync("system", "user"));
        sw.Stop();
        _output.WriteLine(
            $"Llm threw {ex.GetType().Name}: {ex.Message} in {sw.ElapsedMilliseconds}ms"
        );
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Llm fail-fast exceeded {timeoutMs}ms window: {sw.ElapsedMilliseconds}ms"
        );
    }

    [Fact]
    public async Task OllamaLlmClient_HealthProbe_CallerCancelled_ReturnsFalseFast()
    {
        var llm = new OllamaLlmClient("http://127.0.0.1:59997", "phi3:mini", 1500);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sw = Stopwatch.StartNew();
        var healthy = await llm.IsHealthyAsync(cts.Token);
        sw.Stop();
        Assert.False(healthy);
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Caller-cancelled probe should fail fast: {sw.ElapsedMilliseconds}ms"
        );
    }

    [Fact]
    public async Task OllamaEmbedder_TimeoutRespected_NotDefault100s()
    {
        var unreachable = "http://127.0.0.1:59996";
        var timeoutMs = 800;
        var embedder = new OllamaEmbedder(unreachable, "all-minilm", timeoutMs);
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            embedder.GetEmbeddingsAsync(new[] { "a", "b" })
        );
        sw.Stop();
        _output.WriteLine($"800ms timeout elapsed {sw.ElapsedMilliseconds}ms");
        // Must be ~800ms, not 100s default — proves HttpClient.Timeout wired, not hanging.
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(2),
            $"Timeout not wired: elapsed {sw.ElapsedMilliseconds}ms for {timeoutMs}ms timeout"
        );
        Assert.True(
            sw.Elapsed >= TimeSpan.FromMilliseconds(200),
            "Too fast — likely not actually attempting network"
        );
    }
}
