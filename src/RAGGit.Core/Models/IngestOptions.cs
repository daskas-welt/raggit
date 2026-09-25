using System;

namespace RAGGit.Core.Models;

/// <summary>
/// Tunable performance options for the ingestion pipeline.
/// Values are loaded from configuration (<c>Ingest</c> section) and can be
/// adjusted per-deployment without rebuilding.
/// </summary>
public sealed class IngestOptions
{
    /// <summary>
    /// Maximum number of cells read from visible spreadsheet sheets.
    /// Default: 100,000.
    /// </summary>
    public int MaxSpreadsheetCells { get; set; } = DocumentValidation.MaxSpreadsheetCells;

    /// <summary>
    /// Maximum number of chunks embedded in a single call to the embedder.
    /// Smaller values reduce peak memory; larger values improve throughput.
    /// Default: 64.
    /// </summary>
    public int EmbedBatchSize { get; set; } = 64;

    /// <summary>
    /// Chunk size in approximate tokens.
    /// </summary>
    public int ChunkSize { get; set; } = 512;

    /// <summary>
    /// Overlap between consecutive chunks in approximate tokens.
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// Whether to cache extracted chunks keyed by document hash. This avoids
    /// re-chunking if the same file is uploaded again before the cache entry
    /// expires.
    /// </summary>
    public bool EnableChunkCache { get; set; } = true;

    /// <summary>
    /// Absolute expiration for chunk-cache entries.
    /// </summary>
    public TimeSpan ChunkCacheTtl { get; set; } = TimeSpan.FromHours(1);
}
