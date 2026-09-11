using System;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// One search result returned by the vector store.
/// </summary>
public sealed record SearchResult(
    Guid ChunkId,
    string DocumentId,
    string Text,
    int Ordinal,
    float Score
);
