using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Retrieval;

/// <summary>
/// The one owner of the retrieved-source trust rule: a citation shown to a user
/// must be a retrieved chunk, and a document name never leaks an internal id.
/// </summary>
internal static class RetrievedSourceMapper
{
    public static async Task<Dictionary<string, string?>> ResolveDocumentNamesAsync(
        IReadOnlyList<SearchResult> chunks,
        IDocumentRepository documents,
        ILogger? logger,
        CancellationToken cancellationToken
    )
    {
        var names = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var documentId in chunks.Select(chunk => chunk.DocumentId).Distinct())
        {
            if (!Guid.TryParse(documentId, out var id))
            {
                continue;
            }

            try
            {
                var document = await documents.FindByIdAsync(id, cancellationToken);
                names[documentId] = document?.Filename;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger?.LogDebug(
                    exception,
                    "Document name lookup failed for {DocumentId}; using id in prompt",
                    documentId
                );
            }
        }

        return names;
    }

    public static List<Citation> MapCitations(
        IReadOnlyList<SearchResult> chunks,
        IReadOnlyCollection<Guid> citationIds,
        IReadOnlyDictionary<string, string?> documentNames
    )
    {
        var cited = citationIds.ToHashSet();
        return chunks
            .Where(chunk => cited.Contains(chunk.ChunkId))
            .Where(chunk => Guid.TryParse(chunk.DocumentId, out _))
            .Select(chunk => new Citation
            {
                DocumentId = Guid.Parse(chunk.DocumentId),
                DocumentName = documentNames.GetValueOrDefault(chunk.DocumentId),
                ChunkId = chunk.ChunkId,
                Text = chunk.Text,
                Ordinal = chunk.Ordinal,
            })
            .ToList();
    }
}
