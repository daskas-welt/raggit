using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// Stores original uploaded bytes keyed by document id (009). Rows without a
/// stored original (e.g. legacy uploads) read as absent — never an error.
/// </summary>
public interface IDocumentContentStore
{
    /// <summary>
    /// Reads <paramref name="content"/> from its current position to end.
    /// Callers rewind seekable streams first.
    /// </summary>
    Task SaveAsync(Guid documentId, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored bytes, or null when no original was stored.
    /// Caller disposes the returned stream.
    /// </summary>
    Task<Stream?> OpenReadAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes stored bytes if present; missing bytes are not an error.
    /// </summary>
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}
