using System;
using System.Collections.Generic;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// A single vector point to be stored or retrieved from the vector store.
/// </summary>
public sealed record VectorRecord(
    Guid Id,
    float[] Vector,
    IReadOnlyDictionary<string, object?> Payload);
