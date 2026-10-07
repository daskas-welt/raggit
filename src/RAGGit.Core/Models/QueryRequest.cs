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

    /// <summary>
    /// Intent override (030, FR-007): <c>Auto</c> (default) lets the server
    /// classify; <c>Broad</c> forces parent-context retrieval;
    /// <c>Specific</c> forces granular child retrieval. Optional and
    /// backward compatible — omitting it behaves exactly as pre-feature.
    /// </summary>
    public QueryMode Mode { get; set; } = QueryMode.Auto;
}

/// <summary>
/// A citation returned with a grounded answer.
/// </summary>
public sealed class Citation
{
    public Guid DocumentId { get; set; }

    public string? DocumentName { get; set; }

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

    /// <summary>
    /// Effective retrieval intent actually used for this answer (030): equal
    /// to the request override when supplied, else the classifier result.
    /// Optional and additive — pre-feature clients ignore it.
    /// </summary>
    public QueryIntent? Mode { get; set; }

    /// <summary>
    /// Closest person names actually present in retrieved chunks when the
    /// asked name does not match. Surname-anchored hints only; never
    /// transferred facts. Empty when citations exist or no surname matches.
    /// </summary>
    public List<PersonSuggestion> SuggestedPersons { get; set; } = new();
}

/// <summary>
/// A did-you-mean person hint for near-miss name queries.
/// </summary>
public sealed class PersonSuggestion
{
    public string Name { get; set; } = string.Empty;

    public Guid? DocumentId { get; set; }
}
