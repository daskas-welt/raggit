using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Core.Abstractions;

/// <summary>
/// Pluggable virus/malware scanner hook for uploaded documents.
/// Implementations MUST throw <see cref="System.InvalidDataException"/> or
/// return <c>false</c> when a threat is detected so the upload is rejected
/// without touching the index.
/// </summary>
public interface IVirusScanner
{
    /// <summary>
    /// Scans <paramref name="stream"/> and returns <c>true</c> if clean.
    /// The stream is seekable and positioned at 0.
    /// </summary>
    Task<bool> ScanAsync(Stream stream, string filename, CancellationToken cancellationToken = default);
}
