using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IngestService ingestService, ILogger<DocumentsController> logger)
    {
        _ingestService = ingestService ?? throw new ArgumentNullException(nameof(ingestService));
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
            _logger.LogWarning("Upload rejected: file {Filename} size {Size} exceeds 100MB", file.FileName, file.Length);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { error = "File exceeds 100MB." });
        }

        if (!TryMapMime(file.ContentType, file.FileName, out var mime))
        {
            var extension = Path.GetExtension(file.FileName);
            _logger.LogWarning("Upload rejected: unsupported type {Extension} for {Filename}", extension, file.FileName);
            return BadRequest(new { error = $"unsupported type: {extension}" });
        }

        var createdBy = User.Identity?.Name ?? "unknown";
        _logger.LogInformation("Admin uploading {Filename} ({Mime}) as {User}", file.FileName, mime, createdBy);

        await using var stream = file.OpenReadStream();
        var (document, created) = await _ingestService.IngestAsync(
            stream,
            file.FileName,
            mime,
            file.Length,
            createdBy,
            cancellationToken);

        return created
            ? StatusCode(StatusCodes.Status201Created, document)
            : Ok(document);
    }

    /// <summary>
    /// GET /api/documents — list library documents (Admin and Employee).
    /// </summary>
    [HttpGet]
    [AllowAnonymous] // Auth still enforced at controller level; both roles may list.
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var documents = await _ingestService.ListDocumentsAsync(cancellationToken);
        return Ok(documents);
    }

    /// <summary>
    /// DELETE /api/documents/{id} — purge stub for User Story 3.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin requested delete for document {DocumentId}", id);

        // TODO: remove Document + Chunk rows and purge vectors in US3.
        // The vector store already exposes a documentId filter delete wrapper
        // via IVectorStore.DeleteAsync(documentId).
        await Task.CompletedTask;

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
