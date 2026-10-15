using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;
using RAGGit.Retrieval;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Employee + Admin query endpoint per contracts/api.yaml.
/// Both transports delegate to the one <see cref="QueryPipeline"/> (032): the
/// classic request/response path, and the NDJSON stream that narrates the work
/// while the employee waits.
/// </summary>
[ApiController]
[Route("api/queries")]
[Authorize]
public sealed class QueryController : ControllerBase
{
    private readonly QueryPipeline _pipeline;
    private readonly ILlmClient _llmClient;
    private readonly IQueryRepository _historyStore;
    private readonly ILogger<QueryController> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public QueryController(
        QueryPipeline pipeline,
        ILlmClient llmClient,
        IQueryRepository historyStore,
        IOptions<JsonOptions> jsonOptions,
        ILogger<QueryController> logger
    )
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(llmClient);
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(jsonOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _pipeline = pipeline;
        _llmClient = llmClient;
        _historyStore = historyStore;
        _logger = logger;
        _jsonOptions =
            jsonOptions.Value.JsonSerializerOptions
            ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    /// <summary>
    /// GET /api/queries/history — own query history, paginated, sub-scoped (005).
    /// 401 when no person identity (e.g. legacy API-key caller — the ApiKey
    /// handler mints no NameIdentifier/sub claim, so it cannot bypass);
    /// 200 with empty items when the person has no queries.
    /// Isolation (FR-007/FR-008): store filters UserId == sub AND NOT IN
    /// legacy; deactivation/expiry enforced per-request by JwtBearer +
    /// OnTokenValidated (004), never bypassed here.
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        var page = await _historyStore.ListHistoryAsync(sub, limit, offset, cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// GET /api/queries/{id} — full prompt/answer + citations iff owned (005).
    /// 401 when no person identity; 404 when not found, not owned, or legacy
    /// (404 — never 403 — so non-owners cannot enumerate query ids).
    /// The :guid constraint keeps /api/queries/history from matching here.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        var detail = await _historyStore.GetDetailAsync(sub, id, cancellationToken);
        if (detail is null)
        {
            return NotFound(new { error = "not found" });
        }

        return Ok(detail);
    }

    /// <summary>
    /// POST /api/queries — grounded answer with citations or "no relevant content found".
    /// Unchanged in 032: it runs the same pipeline with no progress observer.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Post(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

        try
        {
            if (!await _llmClient.IsHealthyAsync(cancellationToken))
            {
                _logger.LogWarning("Query rejected: LLM is not healthy for user {UserId}", userId);
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "AI workstation unavailable" }
                );
            }

            var outcome = await _pipeline.ExecuteAsync(
                request,
                onProgress: null,
                cancellationToken
            );

            await SaveQueryAsync(
                userId,
                request.Query,
                outcome.RetrievedChunkIds,
                outcome.Answer,
                outcome.CitationIds,
                outcome.LatencyMs,
                outcome.Mode,
                cancellationToken
            );

            if (outcome.RetrievedChunkIds.Count == 0)
            {
                _logger.LogInformation(
                    "Query from {UserId} returned no relevant content in {LatencyMs}ms",
                    userId,
                    outcome.LatencyMs
                );
            }
            else
            {
                _logger.LogInformation(
                    "Query from {UserId} answered with {CitationCount}/{RetrievedCount} citations and {SuggestionCount} suggestions in {LatencyMs}ms",
                    userId,
                    outcome.Citations.Count,
                    outcome.RetrievedChunkIds.Count,
                    outcome.SuggestedPersons.Count,
                    outcome.LatencyMs
                );
            }

            return Ok(outcome.ToResponse());
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Query failed because LLM is unreachable for user {UserId}",
                userId
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout (Ollama:TimeoutMs) — fail fast per R7/FR-007, never hang or pull
            _logger.LogError(
                exception,
                "Query timed out (Ollama:TimeoutMs) for user {UserId}",
                userId
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Query operation canceled (timeout) for user {UserId}",
                userId
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Query failed due to AI workstation error for user {UserId}",
                userId
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "AI workstation unavailable" }
            );
        }
    }

    /// <summary>
    /// POST /api/queries/stream — the same query, narrated live (032 FR-001…FR-004).
    /// Responds with newline-delimited JSON events, flushed as they happen: zero
    /// or more <c>progress</c> events (with the sources consulted) followed by
    /// exactly one terminal <c>answer</c> or <c>error</c>. Validation and the
    /// health probe complete before the first byte, so those failures keep the
    /// ordinary JSON status codes clients already map.
    /// </summary>
    [HttpPost("stream")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task PostStream(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            await WriteJsonStatusAsync(
                StatusCodes.Status400BadRequest,
                "Query is required.",
                cancellationToken
            );
            return;
        }

        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

        if (!await _llmClient.IsHealthyAsync(cancellationToken))
        {
            _logger.LogWarning("Query rejected: LLM is not healthy for user {UserId}", userId);
            await WriteJsonStatusAsync(
                StatusCodes.Status503ServiceUnavailable,
                "AI workstation unavailable",
                cancellationToken
            );
            return;
        }

        // First byte is about to be written: from here on, a failure is reported
        // in-band so the client keeps one error vocabulary (research R10).
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-store";

        try
        {
            var outcome = await _pipeline.ExecuteAsync(
                request,
                WriteProgressAsync,
                cancellationToken
            );

            await SaveQueryAsync(
                userId,
                request.Query,
                outcome.RetrievedChunkIds,
                outcome.Answer,
                outcome.CitationIds,
                outcome.LatencyMs,
                outcome.Mode,
                cancellationToken
            );

            if (outcome.RetrievedChunkIds.Count == 0)
            {
                _logger.LogInformation(
                    "Streamed query from {UserId} returned no relevant content in {LatencyMs}ms",
                    userId,
                    outcome.LatencyMs
                );
            }
            else
            {
                _logger.LogInformation(
                    "Streamed query from {UserId} answered with {CitationCount}/{RetrievedCount} citations and {SuggestionCount} suggestions in {LatencyMs}ms",
                    userId,
                    outcome.Citations.Count,
                    outcome.RetrievedChunkIds.Count,
                    outcome.SuggestedPersons.Count,
                    outcome.LatencyMs
                );
            }

            await WriteEventAsync(
                QueryStreamEvent.FromAnswer(outcome.ToResponse()),
                cancellationToken
            );
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Streamed query failed because LLM is unreachable for user {UserId}",
                userId
            );
            await TryWriteErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                "model unavailable offline"
            );
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Streamed query timed out (Ollama:TimeoutMs) for user {UserId}",
                userId
            );
            await TryWriteErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                "model unavailable offline"
            );
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "Streamed query operation canceled (timeout) for user {UserId}",
                userId
            );
            await TryWriteErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                "model unavailable offline"
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Streamed query failed due to AI workstation error for user {UserId}",
                userId
            );
            await TryWriteErrorAsync(
                StatusCodes.Status503ServiceUnavailable,
                "AI workstation unavailable"
            );
        }
    }

    /// <summary>Writes one progress update and flushes it (liveness, SC-002).</summary>
    private async ValueTask WriteProgressAsync(
        QueryProgress progress,
        CancellationToken cancellationToken
    ) => await WriteEventAsync(QueryStreamEvent.FromProgress(progress), cancellationToken);

    private async Task WriteEventAsync(
        QueryStreamEvent streamEvent,
        CancellationToken cancellationToken
    )
    {
        var line = JsonSerializer.Serialize(streamEvent, _jsonOptions) + "\n";
        await Response.WriteAsync(line, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Reports a post-first-byte failure. A disconnected client makes the write
    /// fail; that is not an error worth surfacing anywhere else.
    /// </summary>
    private async Task TryWriteErrorAsync(int status, string error)
    {
        try
        {
            await WriteEventAsync(
                QueryStreamEvent.FromError(error, status),
                CancellationToken.None
            );
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Stream ended before the failure could be reported");
        }
    }

    private async Task WriteJsonStatusAsync(
        int status,
        string error,
        CancellationToken cancellationToken
    )
    {
        Response.StatusCode = status;
        // Pinned content type: WriteAsJsonAsync would otherwise pick
        // application/problem+json for 4xx, and the classic endpoint answers with
        // application/json — the two transports must look the same (FR-012).
        await Response.WriteAsJsonAsync(
            new { error },
            _jsonOptions,
            "application/json",
            cancellationToken
        );
    }

    private async Task SaveQueryAsync(
        string userId,
        string prompt,
        IReadOnlyList<Guid> retrievedChunkIds,
        string? answer,
        IReadOnlyList<Guid> citationIds,
        int latencyMs,
        QueryIntent? mode,
        CancellationToken cancellationToken
    )
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
            Mode = mode,
            CreatedAt = DateTime.UtcNow,
        };

        await _historyStore.AddAsync(query, cancellationToken);
    }
}
