using System;
using System.ComponentModel.DataAnnotations;

namespace RAGGit.Core.Models;

/// <summary>
/// Singleton library per deployment.
/// </summary>
public sealed class Library
{
    [Range(1, 1, ErrorMessage = "Library is a singleton and must have Id = 1.")]
    public int Id { get; set; } = 1;

    [Required(ErrorMessage = "Library name is required.")]
    public string Name { get; set; } = "RAGGit Library";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
