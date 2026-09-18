using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RAGGit.Core.Models;

/// <summary>
/// Employee query request body per contracts/api.yaml.
/// </summary>
public sealed class QueryRequest
{
    [Required(ErrorMessage = "Query is required.")]
    [MinLength(1, ErrorMessage = "Query cannot be empty.")]
    public string Query { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "topK must be between 1 and 5.")]
    public int TopK { get; set; } = 5;
}

/// <summary>
/// A citation returned with a grounded answer.
/// </summary>
public sealed class Citation
{
    public Guid DocumentId { get; set; }

    public Guid ChunkId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    public int Ordinal { get; set; }
}

/// <summary>
/// Response body for <c>POST /api/queries</c>.
/// </summary>
public sealed class QueryResponse
{
    public string Answer { get; set; } = string.Empty;

    public List<Citation> Citations { get; set; } = new();

    public List<Guid> RetrievedChunkIds { get; set; } = new();

    public int LatencyMs { get; set; }
}
