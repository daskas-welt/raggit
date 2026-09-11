using System;

namespace RAGGit.Ingest;

/// <summary>
/// Indicates the uploaded document is corrupted or invalid and must be
/// rejected with 400 and no partial index (FR-006, SC-005).
/// </summary>
public sealed class CorruptDocumentException : Exception
{
    public CorruptDocumentException(string message)
        : base(message) { }

    public CorruptDocumentException(string message, Exception innerException)
        : base(message, innerException) { }
}
