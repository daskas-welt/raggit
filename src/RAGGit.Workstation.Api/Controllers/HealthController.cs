using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Anonymous health endpoint per contracts/api.yaml.
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly ILlmClient _llmClient;
    private readonly IQueryRepository _queries;

    public HealthController(
        IVectorStore vectorStore,
        ILlmClient llmClient,
        IQueryRepository queries
    )
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>
    /// GET /health — returns vectorDb/llm status, API version, and recent
    /// query latency p95 for SC-002 monitoring.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var vectorDbHealthy = await _vectorStore.IsHealthyAsync(cancellationToken);
        var llmHealthy = await _llmClient.IsHealthyAsync(cancellationToken);
        var p95Latency = await GetP95LatencyAsync(cancellationToken);

        return Ok(
            new
            {
                vectorDb = vectorDbHealthy ? "ok" : "down",
                llm = llmHealthy ? "ok" : "down",
                p95LatencyMs = p95Latency,
                version = GetApiVersion(),
            }
        );
    }

    private async Task<int> GetP95LatencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var latencies = await _queries.GetRecentLatenciesAsync(cancellationToken);
            if (latencies.Count == 0)
            {
                return 0;
            }

            var ordered = latencies.OrderBy(x => x).ToList();
            var index = (int)Math.Ceiling(ordered.Count * 0.95) - 1;
            return ordered[Math.Max(0, index)];
        }
        catch
        {
            return -1;
        }
    }

    private static string GetApiVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
