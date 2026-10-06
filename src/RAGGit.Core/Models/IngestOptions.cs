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
    /// Maximum number of documents processed concurrently by the background
    /// ingestion worker. Default: 2.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 2;

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

    /// <summary>
    /// How often the maintenance worker checks whether the vector store needs
    /// compaction/indexing once ingest has drained. Default: 30 seconds.
    /// </summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// LanceDB <c>Optimize</c> retention: fragments and versions older than
    /// this are compacted and pruned. Default: 7 days.
    /// </summary>
    public TimeSpan MaintenanceCleanupOlderThan { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Row count below which maintenance skips building the vector index
    /// (brute-force search is faster on small tables). Default: 10,000.
    /// </summary>
    public int VectorIndexThresholdRows { get; set; } = 10_000;
}
