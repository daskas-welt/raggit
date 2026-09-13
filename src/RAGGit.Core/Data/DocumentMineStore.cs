using System;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Core.Data;

/// <summary>
/// Per-person paginated reads over the Documents table (005-per-person-history US3).
/// Offline invariant: pure Microsoft.Data.Sqlite reads, no Ollama/LanceDB/HTTP.
/// Same pagination envelope as <see cref="QueryHistoryStore"/>; ListAsync lands in T025.
/// </summary>
public sealed class DocumentMineStore
{
    private readonly RagDbContext _dbContext;

    public DocumentMineStore(RagDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Lists the caller's own documents (CreatedBy == sub, legacy excluded),
    /// ordered CreatedAt DESC, Id DESC. Implemented in T025.
    /// </summary>
    public Task<DocumentsMinePage> ListAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        throw new NotImplementedException("Implemented in T025.");
    }

    internal const string ListSql =
        @"
            SELECT Id, Filename, Size, Status, CreatedAt
            FROM Documents
            WHERE CreatedBy = @sub AND CreatedBy NOT IN ('admin','employee')
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT @limit OFFSET @offset;";

    internal const string CountSql =
        @"
            SELECT COUNT(*)
            FROM Documents
            WHERE CreatedBy = @sub AND CreatedBy NOT IN ('admin','employee');";
}
