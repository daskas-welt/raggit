using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest;

namespace RAGGit.Workstation.Api.Controllers;

/// <summary>
/// Admin document upload and library listing endpoints per contracts/api.yaml.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IngestService _ingestService;
    private readonly IVirusScanner _virusScanner;
    private readonly DocumentMineStore _mineStore;
    private readonly IDocumentContentStore _contentStore;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IngestService ingestService,
        IVirusScanner virusScanner,
        DocumentMineStore mineStore,
        IDocumentContentStore contentStore,
        ILogger<DocumentsController> logger
    )
    {
        _ingestService = ingestService ?? throw new ArgumentNullException(nameof(ingestService));
        _virusScanner = virusScanner ?? throw new ArgumentNullException(nameof(virusScanner));
        _mineStore = mineStore ?? throw new ArgumentNullException(nameof(mineStore));
        _contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// POST /api/documents — upload a supported document (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(DocumentValidation.MaxFileSizeBytes + 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            _logger.LogWarning("Upload rejected: missing or empty file");
            return BadRequest(new { error = "File is required." });
        }

        if (file.Length > DocumentValidation.MaxFileSizeBytes)
        {
            _logger.LogWarning(
                "Upload rejected: file {Filename} size {Size} exceeds 100MB",
                file.FileName,
                file.Length
            );
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { error = "File exceeds 100MB." }
            );
        }

        if (!TryMapMime(file.ContentType, file.FileName, out var mime))
        {
            var extension = Path.GetExtension(file.FileName);
            _logger.LogWarning(
                "Upload rejected: unsupported type {Extension} for {Filename}",
                extension,
                file.FileName
            );
            return BadRequest(new { error = $"unsupported type: {extension}" });
        }

        var createdBy =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";
        var createdByName =
            User.FindFirst("displayName")?.Value
            ?? User.FindFirst("username")?.Value
            ?? User.Identity?.Name;
        _logger.LogInformation(
            "Admin uploading {Filename} ({Mime}) as {User}",
            file.FileName,
            mime,
            createdBy
        );

        try
        {
            await using var stream = file.OpenReadStream();
            var validatedStream = await DocumentFormatValidator.ValidateAndRewindAsync(
                stream,
                mime,
                cancellationToken
            );

            var scanPassed = await _virusScanner.ScanAsync(
                validatedStream,
                file.FileName,
                cancellationToken
            );
            if (!scanPassed)
            {
                _logger.LogWarning(
                    "Upload rejected: virus scan failed for {Filename}",
                    file.FileName
                );
                return BadRequest(new { error = "File failed security scan." });
            }

            var (document, created) = await _ingestService.IngestAsync(
                validatedStream,
                file.FileName,
                mime,
                file.Length,
                createdBy,
                createdByName,
                cancellationToken
            );

            return created ? StatusCode(StatusCodes.Status201Created, document) : Ok(document);
        }
        catch (SpreadsheetCellCapExceededException ex)
        {
            _logger.LogWarning(ex, "Upload rejected: cap exceeded {Filename}", file.FileName);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = ex.Message });
        }
        catch (NoExtractableContentException ex)
        {
            _logger.LogWarning(
                ex,
                "Upload rejected: no extractable content {Filename}",
                file.FileName
            );
            return BadRequest(new { error = ex.Message });
        }
        catch (CorruptDocumentException ex)
        {
            _logger.LogWarning(ex, "Upload rejected: corrupted document {Filename}", file.FileName);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Upload rejected: invalid data {Filename}", file.FileName);
            var msg =
                ex.Message.Contains("corrupted", StringComparison.OrdinalIgnoreCase) ? ex.Message
                : mime == DocumentMimeType.Pdf ? "corrupted pdf"
                : mime == DocumentMimeType.Docx ? "corrupted docx"
                : mime == DocumentMimeType.Xlsx ? "corrupted xlsx"
                : "corrupted document";
            return BadRequest(new { error = msg });
        }
    }

    /// <summary>
    /// GET /api/documents/{id}/content — original file bytes (009).
    /// Any authenticated caller (same bar as the list endpoints); legacy rows
    /// without stored bytes get 404, never an empty 200 or anonymous bytes.
    /// </summary>
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken cancellationToken)
    {
        var document = await _ingestService.FindDocumentByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return NotFound(new { error = "Document not found." });
        }

        var stream = await _contentStore.OpenReadAsync(id, cancellationToken);
        if (stream is null)
        {
            return NotFound(new { error = "original unavailable" });
        }

        Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition
        {
            Inline = true,
            FileName = document.Filename,
        }.ToString();
        return File(stream, document.Mime.GetContentType());
    }

    /// <summary>
    /// GET /api/documents/mine — own recent documents, paginated, sub-scoped (005).
    /// 401 when no person identity; 200 with empty items when the person
    /// uploaded nothing. Isolation (FR-005/FR-007): store filters
    /// CreatedBy == sub AND NOT IN legacy; deactivation/expiry enforced
    /// per-request by JwtBearer + OnTokenValidated (004), never bypassed here.
    /// </summary>
    [HttpGet("/api/documents/mine")]
    public async Task<IActionResult> Mine(
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

        var page = await _mineStore.ListAsync(sub, limit, offset, cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// GET /api/documents — list library documents (Admin and Employee).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var documents = await _ingestService.ListDocumentsAsync(cancellationToken);
        return Ok(documents);
    }

    /// <summary>
    /// DELETE /api/documents/{id} — purge document, chunks, and vectors (Admin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin requested delete for document {DocumentId}", id);

        var deleted = await _ingestService.DeleteDocumentAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { error = "Document not found." });
        }

        return NoContent();
    }

    private static bool TryMapMime(string? contentType, string fileName, out DocumentMimeType mime)
    {
        mime = default;

        // Prefer the declared content type, stripping charset if present.
        var declared = contentType?.Split(';').FirstOrDefault()?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(declared))
        {
            switch (declared)
            {
                case "application/pdf":
                    mime = DocumentMimeType.Pdf;
                    return true;
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                    mime = DocumentMimeType.Docx;
                    return true;
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                    mime = DocumentMimeType.Xlsx;
                    return true;
                case "text/plain":
                    mime = DocumentMimeType.Txt;
                    return true;
                case "text/markdown":
                    mime = DocumentMimeType.Md;
                    return true;
            }
        }

        // Fall back to file extension.
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        switch (extension)
        {
            case ".pdf":
                mime = DocumentMimeType.Pdf;
                return true;
            case ".docx":
                mime = DocumentMimeType.Docx;
                return true;
            case ".xlsx":
                mime = DocumentMimeType.Xlsx;
                return true;
            case ".txt":
                mime = DocumentMimeType.Txt;
                return true;
            case ".md":
                mime = DocumentMimeType.Md;
                return true;
        }

        return false;
    }
}
