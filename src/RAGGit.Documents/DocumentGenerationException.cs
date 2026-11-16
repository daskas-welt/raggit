using System;

namespace RAGGit.Documents;

/// <summary>Why a document request could not be produced; mapped to HTTP by the API.</summary>
public enum DocumentGenerationError
{
    NotEligible,
    TooLarge,
    UnsupportedFormat,
}

/// <summary>User-safe document generation failure; carries no answer content.</summary>
public sealed class DocumentGenerationException : Exception
{
    public DocumentGenerationException(DocumentGenerationError error, string message)
        : base(message)
    {
        Error = error;
    }

    public DocumentGenerationError Error { get; }
}
