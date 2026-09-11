using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Tests.Integration.Helpers;

/// <summary>
/// Probe helper for opt-in real-Ollama suites (R5, T013).
/// Probes GET {Ollama:Url}/api/tags with a short timeout so CI without Ollama
/// stays green — tests return early (graceful skip) when Ollama absent (SC-003).
/// </summary>
public static class OllamaProbe
{
    /// <summary>
    /// Returns true when Ollama is reachable at <paramref name="baseUrl"/> within <paramref name="timeoutMs"/>.
    /// Never throws — returns false on any failure (unreachable, timeout, non-success).
    /// </summary>
    public static async Task<bool> IsAvailableAsync(
        string baseUrl,
        int timeoutMs = 1500,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        var url = baseUrl.TrimEnd('/') + "/api/tags";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            var response = await client.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads Ollama:Url from environment or falls back to http://localhost:11434.
    /// Integration tests override via IConfiguration when using WebApplicationFactory.
    /// </summary>
    public static string DefaultUrl =>
        Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
}
