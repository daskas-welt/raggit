using System;

namespace RAGGit.Ingest;

public sealed class NoExtractableContentException : Exception
{
    public NoExtractableContentException(string message) : base(message) { }
    public NoExtractableContentException(string message, Exception inner) : base(message, inner) { }
}
