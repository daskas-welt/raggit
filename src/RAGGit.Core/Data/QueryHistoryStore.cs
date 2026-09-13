using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    /// Returns the full prompt/answer plus citations ordered by ordinal for one
    /// owned query, or null when not found / not owned / legacy.
    /// Ownership and legacy exclusion are enforced in SQL (FR-004/FR-007/FR-008).
    /// Offline: SQLite-only (Queries + Chunks reads).
    /// </summary>
    public async Task<QueryDetail?> GetDetailAsync(
        string sub,
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        string prompt;
        string answer;
        List<Guid> citationIds;
        int latencyMs;
        DateTime createdAt;

        using (var detailCommand = connection.CreateCommand())
        {
            detailCommand.CommandText = DetailSql;
            detailCommand.Parameters.AddWithValue("@id", id.ToString());
            detailCommand.Parameters.AddWithValue("@sub", sub);

            await using var reader = await detailCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            prompt = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            answer = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            citationIds = ParseGuidList(reader.IsDBNull(2) ? null : reader.GetString(2));
            latencyMs = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            createdAt = DateTime.Parse(
                reader.GetString(4),
                null,
                System.Globalization.DateTimeStyles.RoundtripKind
            );
        }

        var citations = new List<HistoryCitation>();
        if (citationIds.Count > 0)
        {
            var ordinals = new Dictionary<Guid, int>();
            var placeholders = new List<string>();
            for (var i = 0; i < citationIds.Count; i++)
            {
                var name = $"@c{i}";
                placeholders.Add(name);
                ordinals[citationIds[i]] = i;
            }

            using var chunksCommand = connection.CreateCommand();
            chunksCommand.CommandText =
                "SELECT Id, DocumentId, Ordinal, Text FROM Chunks WHERE Id IN ("
                + string.Join(",", placeholders)
                + ");";
            for (var i = 0; i < citationIds.Count; i++)
            {
                chunksCommand.Parameters.AddWithValue($"@c{i}", citationIds[i].ToString());
            }

            await using var chunkReader = await chunksCommand.ExecuteReaderAsync(cancellationToken);
            while (await chunkReader.ReadAsync(cancellationToken))
            {
                var chunkId = Guid.Parse(chunkReader.GetString(0));
                citations.Add(
                    new HistoryCitation
                    {
                        DocumentId = Guid.Parse(chunkReader.GetString(1)),
                        ChunkId = chunkId,
                        Text = chunkReader.IsDBNull(3) ? string.Empty : chunkReader.GetString(3),
                        Ordinal = chunkReader.IsDBNull(2)
                            ? ordinals.GetValueOrDefault(chunkId)
                            : chunkReader.GetInt32(2),
                    }
                );
            }

            citations = citations.OrderBy(c => c.Ordinal).ToList();
        }

        return new QueryDetail
        {
            Id = id,
            Prompt = prompt,
            Answer = answer,
            Citations = citations,
            LatencyMs = latencyMs,
            CreatedAt = createdAt,
        };
    }

    private static List<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            var guids = JsonSerializer.Deserialize<List<Guid>>(json);
            return guids ?? new List<Guid>();
        }
        catch (JsonException)
        {
            return new List<Guid>();
        }
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

    internal const string DetailSql =
        @"
            SELECT Prompt, Answer, CitationIds, LatencyMs, CreatedAt
            FROM Queries
            WHERE Id = @id AND UserId = @sub AND UserId NOT IN ('admin','employee');";

    internal static bool IsLegacyUserId(string userId) => LegacyUserIds.Contains(userId);
}
