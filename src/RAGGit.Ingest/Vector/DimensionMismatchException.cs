using System;

namespace RAGGit.Ingest.Vector;

/// <summary>
/// Thrown when configured VectorSize does not match the persisted LanceDB collection dimension.
/// </summary>
public sealed class DimensionMismatchException : InvalidOperationException
{
    public int ConfiguredDimension { get; }
    public int StoredDimension { get; }

    public DimensionMismatchException(int configuredDimension, int storedDimension)
        : base(
            $"Configured VectorSize {configuredDimension} does not match existing collection dimension {storedDimension} — delete data/lancedb or re-index/migrate"
        )
    {
        ConfiguredDimension = configuredDimension;
        StoredDimension = storedDimension;
    }
}
