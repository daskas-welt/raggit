using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using RAGGit.Documents;
using RAGGit.Retrieval;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// 034: creates a new document from a description. The request and the rendered
/// bytes are transient; nothing is written to query history or the library.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentCreationController : ControllerBase
{
    private readonly DocumentCreationPipeline _pipeline;
    private readonly DocumentGenerator _generator;
    private readonly ILlmClient _llmClient;
    private readonly ILogger<DocumentCreationController> _logger;

    public DocumentCreationController(
        DocumentCreationPipeline pipeline,
        DocumentGenerator generator,
        ILlmClient llmClient,
        ILogger<DocumentCreationController> logger
    )
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>POST /api/documents/generate — one new TXT/PDF/DOCX/XLSX file.</summary>
    [HttpPost("generate")]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> Generate(
        [FromBody] DocumentCreationRequest? request,
        CancellationToken cancellationToken
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "description is required" });
        }

        if (request.Description.Length > DocumentCreationRequest.MaxDescriptionLength)
        {
            return BadRequest(new { error = "description is too long" });
        }

        if (!request.TryGetFormat(out var format))
        {
            return BadRequest(new { error = "unsupported format: use docx, xlsx, pdf or txt" });
        }

        try
        {
            if (!await _llmClient.IsHealthyAsync(cancellationToken))
            {
                _logger.LogWarning("Document creation rejected: local model is not healthy");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "AI workstation unavailable" }
                );
            }

            var draft = await _pipeline.CreateAsync(request, cancellationToken);
            var document = _generator.Generate(draft, format);

            Response.Headers.CacheControl = "no-store";
            _logger.LogInformation(
                "Created {Format} document from a description: {Bytes} bytes, {Sources} sources",
                format.WireValue(),
                document.ByteCount,
                document.Sources.Count
            );

            return File(document.Bytes, document.ContentType, document.FileName);
        }
        catch (DocumentCreationException exception)
        {
            _logger.LogInformation("Document creation rejected: {Error}", exception.Error);
            return UnprocessableEntity(new { error = exception.Message });
        }
        catch (DocumentGenerationException exception)
        {
            _logger.LogInformation("Document rendering rejected: {Error}", exception.Error);
            return exception.Error == DocumentGenerationError.UnsupportedFormat
                ? BadRequest(new { error = "unsupported format" })
                : UnprocessableEntity(new { error = exception.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document creation cancelled by the client");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(
                exception,
                "Document creation failed because the local model is unreachable"
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Document creation timed out (Ollama:TimeoutMs)");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Document creation operation timed out");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "model unavailable offline" }
            );
        }
        catch (Exception exception)
        {
            // Never expose renderer or model internals in the response.
            _logger.LogError(exception, "Document creation failed");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "document generation failed" }
            );
        }
    }
}
