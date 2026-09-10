using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAGGit.Core.Abstractions;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Anonymous health endpoint per contracts/api.yaml.
/// </summary>
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly ILlmClient _llmClient;

    public HealthController(IVectorStore vectorStore, ILlmClient llmClient)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    /// <summary>
    /// GET /health — returns qdrant/llm status and API version.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var qdrantHealthy = await _vectorStore.IsHealthyAsync(cancellationToken);
        var llmHealthy = await _llmClient.IsHealthyAsync(cancellationToken);

        return Ok(new
        {
            qdrant = qdrantHealthy ? "ok" : "down",
            llm = llmHealthy ? "ok" : "down",
            version = GetApiVersion()
        });
    }

    private static string GetApiVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString(3) ?? "1.0.0";
    }
}
