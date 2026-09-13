using System;
using System.Collections.Generic;
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
    /// ordered CreatedAt DESC, Id DESC. Offline: SQLite-only, no Ollama/LanceDB/HTTP.
    /// </summary>
    public async Task<DocumentsMinePage> ListAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        // Same pagination envelope as history (data-model.md): clamp, COUNT, page.
        var clampedLimit = QueryHistoryStore.ClampLimit(limit);
        var clampedOffset = QueryHistoryStore.ClampOffset(offset);

        // offline: SQLite-only read over the Documents table.
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        int total;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = CountSql;
            countCommand.Parameters.AddWithValue("@sub", sub);
            total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        }

        var items = new List<DocumentMineItem>();
        using (var listCommand = connection.CreateCommand())
        {
            listCommand.CommandText = ListSql;
            listCommand.Parameters.AddWithValue("@sub", sub);
            listCommand.Parameters.AddWithValue("@limit", clampedLimit);
            listCommand.Parameters.AddWithValue("@offset", clampedOffset);

            await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(
                    new DocumentMineItem
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        Filename = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Size = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        CreatedAt = DateTime.Parse(
                            reader.GetString(4),
                            null,
                            System.Globalization.DateTimeStyles.RoundtripKind
                        ),
                    }
                );
            }
        }

        return new DocumentsMinePage
        {
            Items = items,
            Total = total,
            Limit = clampedLimit,
            Offset = clampedOffset,
        };
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
