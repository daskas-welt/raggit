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

    /// <summary>
    /// Two-level granularity (030): <see cref="ChunkLevel.Child"/> chunks are
    /// embedded and searched; <see cref="ChunkLevel.Parent"/> chunks group
    /// several children for broad-context retrieval and citations.
    /// </summary>
    public ChunkLevel Level { get; set; } = ChunkLevel.Child;

    /// <summary>
    /// The owning parent chunk's id for a child chunk; always <c>null</c>
    /// for a parent chunk.
    /// </summary>
    public Guid? ParentId { get; set; }
}
