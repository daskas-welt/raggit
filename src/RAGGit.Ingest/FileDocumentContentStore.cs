using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest;

/// <summary>
/// File-system <see cref="IDocumentContentStore"/>: one file per document id
/// under a dedicated directory (never filename-derived: no traversal surface).
/// </summary>
public sealed class FileDocumentContentStore : IDocumentContentStore
{
    private readonly string _directory;

    public FileDocumentContentStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public async Task SaveAsync(
        Guid documentId,
        Stream content,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        await using var file = new FileStream(
            PathFor(documentId),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            useAsync: true
        );
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        var path = PathFor(documentId);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            useAsync: true
        );
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(documentId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(Guid documentId) =>
        System.IO.Path.Combine(_directory, $"{documentId:N}.bin");
}
