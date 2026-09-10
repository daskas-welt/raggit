using System;
using System.ComponentModel.DataAnnotations;

namespace RAGGit.Core.Models;

/// <summary>
/// A text segment derived from a document, used as the unit for embedding.
/// </summary>
public sealed class Chunk
{
    public Guid Id { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Range(0, int.MaxValue)]
    public int Ordinal { get; set; }

    [Required(ErrorMessage = "Chunk text is required.")]
    public string Text { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int TokenCount { get; set; }
}
