using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Retrieval;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Employee query endpoint per contracts/api.yaml.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class QueryController : ControllerBase
{
    private readonly RetrievalService _retrievalService;
    private readonly GenerationService _generationService;
    private readonly ILlmClient _llmClient;
    private readonly RagDbContext _dbContext;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        RetrievalService retrievalService,
        GenerationService generationService,
        ILlmClient llmClient,
        RagDbContext dbContext,
        ILogger<QueryController> logger)
    {
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// POST /api/query — grounded answer with citations or "no relevant content found".
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> Post([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        var topK = Math.Clamp(request.TopK, 1, 5);
        var userId = User.Identity?.Name ?? "unknown";

        try
        {
            if (!await _llmClient.IsHealthyAsync(cancellationToken))
            {
                _logger.LogWarning("Query rejected: LLM is not healthy for user {UserId}", userId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "AI workstation unavailable" });
            }

            var chunks = await _retrievalService.RetrieveAsync(request.Query, topK, cancellationToken);

            if (chunks.Count == 0)
            {
                var noContentLatency = (int)stopwatch.ElapsedMilliseconds;
                await SaveQueryAsync(
                    userId,
                    request.Query,
                    Array.Empty<Guid>(),
                    "no relevant content found",
                    Array.Empty<Guid>(),
                    noContentLatency,
                    cancellationToken);

                _logger.LogInformation(
                    "Query from {UserId} returned no relevant content in {LatencyMs}ms",
                    userId,
                    noContentLatency);

                return Ok(new
                {
                    answer = "no relevant content found",
                    citations = Array.Empty<object>()
                });
            }

            var (answer, citationIds) = await _generationService.GenerateAsync(
                request.Query,
                chunks,
                cancellationToken);

            var latencyMs = (int)stopwatch.ElapsedMilliseconds;
            var retrievedChunkIds = chunks.Select(c => c.ChunkId).ToList();

            await SaveQueryAsync(
                userId,
                request.Query,
                retrievedChunkIds,
                answer,
                citationIds,
                latencyMs,
                cancellationToken);

            var citations = chunks
                .Where(c => citationIds.Contains(c.ChunkId))
                .Select(c => new Citation
                {
                    DocumentId = Guid.Parse(c.DocumentId),
                    ChunkId = c.ChunkId,
                    Text = c.Text,
                    Ordinal = c.Ordinal
                })
                .ToList();

            _logger.LogInformation(
                "Query from {UserId} answered with {CitationCount} citations in {LatencyMs}ms",
                userId,
                citations.Count,
                latencyMs);

            return Ok(new QueryResponse
            {
                Answer = answer,
                Citations = citations,
                RetrievedChunkIds = retrievedChunkIds,
                LatencyMs = latencyMs
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Query failed because LLM is unreachable for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "model unavailable offline" });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout (Ollama:TimeoutMs) — fail fast per R7/FR-007, never hang or pull
            _logger.LogError(exception, "Query timed out (Ollama:TimeoutMs) for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "model unavailable offline" });
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Query operation canceled (timeout) for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "model unavailable offline" });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Query failed due to AI workstation error for user {UserId}", userId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "AI workstation unavailable" });
        }
    }

    private async Task SaveQueryAsync(
        string userId,
        string prompt,
        IReadOnlyList<Guid> retrievedChunkIds,
        string? answer,
        IReadOnlyList<Guid> citationIds,
        int latencyMs,
        CancellationToken cancellationToken)
    {
        var query = new Query
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Prompt = prompt,
            RetrievedChunkIds = retrievedChunkIds,
            Answer = answer,
            CitationIds = citationIds,
            LatencyMs = latencyMs,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.InsertQueryAsync(query, cancellationToken);
    }
}
