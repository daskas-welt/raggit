using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;
using RAGGit.Retrieval;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Employee + Admin query endpoint per contracts/api.yaml.
/// </summary>
[ApiController]
[Route("api/queries")]
[Authorize]
public sealed class QueryController : ControllerBase
{
    private readonly RetrievalService _retrievalService;
    private readonly GenerationService _generationService;
    private readonly ILlmClient _llmClient;
    private readonly IQueryRepository _historyStore;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        RetrievalService retrievalService,
        GenerationService generationService,
        ILlmClient llmClient,
        IQueryRepository historyStore,
        IDocumentRepository documents,
        ILogger<QueryController> logger
    )
    {
        ArgumentNullException.ThrowIfNull(retrievalService);
        ArgumentNullException.ThrowIfNull(generationService);
        ArgumentNullException.ThrowIfNull(llmClient);
        ArgumentNullException.ThrowIfNull(historyStore);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(logger);
        _retrievalService = retrievalService;
        _generationService = generationService;
        _llmClient = llmClient;
        _historyStore = historyStore;
        _documents = documents;
        _logger = logger;
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
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Post(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        if (request is null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        var topK = Math.Clamp(request.TopK, 1, 5);
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

            var chunks = await _retrievalService.RetrieveAsync(
                request.Query,
                topK,
                request.Mode,
                cancellationToken
            );

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
                    cancellationToken
                );

                _logger.LogInformation(
                    "Query from {UserId} returned no relevant content in {LatencyMs}ms",
                    userId,
                    noContentLatency
                );

                return Ok(
                    new { answer = "no relevant content found", citations = Array.Empty<object>() }
                );
            }

            var documentNames = await ResolveDocumentNamesAsync(chunks, cancellationToken);

            var (answer, citationIds) = await _generationService.GenerateAsync(
                request.Query,
                chunks,
                documentNames,
                cancellationToken
            );

            var latencyMs = (int)stopwatch.ElapsedMilliseconds;
            var retrievedChunkIds = chunks.Select(c => c.ChunkId).ToList();

            // Strict grounding is preserved: when the LLM cites nothing (e.g.
            // near-miss person name like Αρετής vs Πηνελόπης), offer
            // surname-anchored did-you-mean hints from retrieved chunks only.
            var suggestions = new List<PersonSuggestion>();
            if (citationIds.Count == 0)
            {
                suggestions = PersonNameSuggester.Suggest(request.Query, chunks).ToList();
            }

            var finalAnswer = answer;
            if (suggestions.Count > 0 && AnswerLanguage.IsRefusalLike(answer))
            {
                var names = string.Join("; ", suggestions.Select(s => s.Name));
                finalAnswer = AnswerLanguage.IsGreek(request.Query)
                    ? $"{answer} Μήπως εννοούσατε: {names}; Παρακαλώ επιβεβαιώστε."
                    : $"{answer} Did you mean: {names}? Please confirm.";
            }

            await SaveQueryAsync(
                userId,
                request.Query,
                retrievedChunkIds,
                finalAnswer,
                citationIds,
                latencyMs,
                cancellationToken
            );

            var citations = chunks
                .Where(c => citationIds.Contains(c.ChunkId))
                .Where(c => Guid.TryParse(c.DocumentId, out _))
                .Select(c => new Citation
                {
                    DocumentId = Guid.Parse(c.DocumentId),
                    DocumentName = documentNames.GetValueOrDefault(c.DocumentId),
                    ChunkId = c.ChunkId,
                    Text = c.Text,
                    Ordinal = c.Ordinal,
                })
                .ToList();

            _logger.LogInformation(
                "Query from {UserId} answered with {CitationCount}/{RetrievedCount} citations and {SuggestionCount} suggestions in {LatencyMs}ms; top scores [{Scores}]",
                userId,
                citations.Count,
                chunks.Count,
                suggestions.Count,
                latencyMs,
                string.Join(", ", chunks.Take(5).Select(c => c.Score.ToString("0.###")))
            );

            return Ok(
                new QueryResponse
                {
                    Answer = finalAnswer,
                    Citations = citations,
                    RetrievedChunkIds = retrievedChunkIds,
                    LatencyMs = latencyMs,
                    SuggestedPersons = suggestions,
                }
            );
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

    private async Task<Dictionary<string, string?>> ResolveDocumentNamesAsync(
        IReadOnlyList<SearchResult> chunks,
        CancellationToken cancellationToken
    )
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var documentId in chunks.Select(c => c.DocumentId).Distinct())
        {
            if (!Guid.TryParse(documentId, out var id))
            {
                continue;
            }

            try
            {
                var document = await _documents.FindByIdAsync(id, cancellationToken);
                names[documentId] = document?.Filename;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "Document name lookup failed for {DocumentId}; using id in prompt",
                    documentId
                );
            }
        }

        return names;
    }

    private async Task SaveQueryAsync(
        string userId,
        string prompt,
        IReadOnlyList<Guid> retrievedChunkIds,
        string? answer,
        IReadOnlyList<Guid> citationIds,
        int latencyMs,
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
            CreatedAt = DateTime.UtcNow,
        };

        await _historyStore.AddAsync(query, cancellationToken);
    }
}
