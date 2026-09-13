using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Core.Data;

/// <summary>
/// Per-person paginated reads over the Queries table (005-per-person-history).
/// Offline invariant: pure Microsoft.Data.Sqlite reads, no Ollama/LanceDB/HTTP.
/// No DDL here; T008 implements ListAsync, optional composite index only if
/// EXPLAIN QUERY PLAN shows SCAN TABLE (research R5).
/// </summary>
public sealed class QueryHistoryStore
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    public const int PreviewMaxLength = 120;

    private static readonly IReadOnlySet<string> LegacyUserIds = new HashSet<string>(
        StringComparer.Ordinal
    )
    {
        "admin",
        "employee",
    };

    private readonly RagDbContext _dbContext;

    public QueryHistoryStore(RagDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Clamps limit to 1..100, defaulting to 20 for null/out-of-range low values.
    /// </summary>
    public static int ClampLimit(int? limit)
    {
        if (limit is null || limit < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }

    /// <summary>
    /// Clamps offset to >= 0, defaulting to 0 for null/negative values.
    /// </summary>
    public static int ClampOffset(int? offset)
    {
        if (offset is null || offset < 0)
        {
            return 0;
        }

        return offset.Value;
    }

    /// <summary>
    /// Truncates to 120 chars plus '…' (max 121 per contracts/api.yaml 1.4.0).
    /// Mirrors the SQL CASE/SUBSTR projection used in ListAsync.
    /// </summary>
    public static string TruncatePreview(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length > PreviewMaxLength ? value.Substring(0, PreviewMaxLength) + "…" : value;
    }

    /// <summary>
    /// Lists the caller's own queries (UserId == sub, legacy excluded),
    /// ordered CreatedAt DESC, Id DESC. Offline: SQLite-only.
    /// </summary>
    public async Task<HistoryPage> ListAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        var clampedLimit = ClampLimit(limit);
        var clampedOffset = ClampOffset(offset);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        int total;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = CountSql;
            countCommand.Parameters.AddWithValue("@sub", sub);
            total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        }

        var items = new List<HistoryItem>();
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
                    new HistoryItem
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        PromptPreview = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        AnswerPreview = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        CitationCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                        LatencyMs = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        CreatedAt = DateTime.Parse(
                            reader.GetString(5),
                            null,
                            System.Globalization.DateTimeStyles.RoundtripKind
                        ),
                    }
                );
            }
        }

        return new HistoryPage
        {
            Items = items,
            Total = total,
            Limit = clampedLimit,
            Offset = clampedOffset,
        };
    }

    /// <summary>
    /// Runs EXPLAIN QUERY PLAN for the history list query so T008 can decide
    /// whether the optional IX_Queries_UserId_CreatedAt_Id index is needed.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetExplainPlanAsync(
        string sub,
        int limit,
        int offset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + ListSql;
        command.Parameters.AddWithValue("@sub", sub);
        command.Parameters.AddWithValue("@limit", limit);
        command.Parameters.AddWithValue("@offset", offset);

        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            plan.Add(reader.GetString(3));
        }

        return plan;
    }

    internal const string ListSql =
        @"
            SELECT Id,
                CASE WHEN length(Prompt) > 120 THEN substr(Prompt, 1, 120) || '…' ELSE Prompt END,
                CASE WHEN length(Answer) > 120 THEN substr(Answer, 1, 120) || '…' ELSE Answer END,
                json_array_length(COALESCE(CitationIds, '[]')),
                LatencyMs, CreatedAt
            FROM Queries
            WHERE UserId = @sub AND UserId NOT IN ('admin','employee')
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT @limit OFFSET @offset;";

    internal const string CountSql =
        @"
            SELECT COUNT(*)
            FROM Queries
            WHERE UserId = @sub AND UserId NOT IN ('admin','employee');";

    internal static bool IsLegacyUserId(string userId) => LegacyUserIds.Contains(userId);
}
