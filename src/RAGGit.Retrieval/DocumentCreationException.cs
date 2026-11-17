using System;

namespace RAGGit.Retrieval;

/// <summary>Why a description-based document could not be produced safely.</summary>
public enum DocumentCreationError
{
    NotGroundable,
}

/// <summary>User-safe creation failure; carries no generated content.</summary>
public sealed class DocumentCreationException : Exception
{
    public DocumentCreationException(DocumentCreationError error, string message)
        : base(message)
    {
        Error = error;
    }

    public DocumentCreationError Error { get; }
}
