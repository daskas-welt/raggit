using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Ingest;

/// <summary>
/// Extracts plain text from supported document formats and splits it into
/// overlapping token windows per data-model.md.
/// </summary>
public static class Chunker
{
    /// <summary>
    /// Extracts raw text from a stream based on the document MIME type.
    /// </summary>
    public static string ExtractText(Stream stream, DocumentMimeType mime)
    {
        throw new NotImplementedException("Text extraction will be implemented in T017.");
    }

    /// <summary>
    /// Splits text into chunks of <paramref name="chunkSize"/> tokens with
    /// <paramref name="overlap"/> tokens overlapping between consecutive chunks.
    /// Token counting is a whitespace-split approximation for v1.
    /// </summary>
    public static IReadOnlyList<Chunk> ChunkText(
        string text,
        Guid documentId,
        int chunkSize = 512,
        int overlap = 50)
    {
        // Stub: returns an empty list so T016 tests fail before T017 implementation.
        return new List<Chunk>();
    }

    /// <summary>
    /// Computes a SHA-256 hash for the stream contents.
    /// </summary>
    public static async Task<string> ComputeHashAsync(Stream stream)
    {
        // Stub: returns an empty hash so T016 tests fail before T017 implementation.
        return string.Empty;
    }
}
