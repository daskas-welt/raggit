using System;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// One search result returned by the vector store.
/// <c>ParentId</c> (030) carries the owning parent chunk's id from the
/// vector payload when present, so broad retrieval can group child hits
/// into their parents; it is informational for granular retrieval.
/// </summary>
public sealed record SearchResult(
    Guid ChunkId,
    string DocumentId,
    string Text,
    int Ordinal,
    float Score,
    string? ParentId = null
);
