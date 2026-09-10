using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RAGGit.Core.Models;

/// <summary>
/// A natural-language query submitted by an employee and its grounded response.
/// </summary>
public sealed class Query
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "UserId is required.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Prompt is required.")]
    [MinLength(1, ErrorMessage = "Prompt cannot be empty.")]
    public string Prompt { get; set; } = string.Empty;

    public IReadOnlyList<Guid> RetrievedChunkIds { get; set; } = Array.Empty<Guid>();

    public string? Answer { get; set; }

    public IReadOnlyList<Guid> CitationIds { get; set; } = Array.Empty<Guid>();

    [Range(0, int.MaxValue)]
    public int LatencyMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
