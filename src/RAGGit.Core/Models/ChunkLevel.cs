namespace RAGGit.Core.Models;

/// <summary>
/// Two-level chunk granularity for intent-aware retrieval (030).
/// <see cref="Child"/> chunks (~512 tokens) are embedded and searched;
/// <see cref="Parent"/> chunks (groups of children, ~2048 tokens) supply
/// broad context and are never embedded.
/// </summary>
public enum ChunkLevel
{
    Child = 0,
    Parent = 1,
}
