using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Core.Abstractions.Repositories;

/// <summary>
/// Repository for the <see cref="Query"/> aggregate root, per the
/// MS persistence-layer design (one repository per aggregate root;
/// interfaces in the domain, implementations in persistence).
/// Reads stay plain SQLite (CQRS side-query style); writes go only
/// through this repository — never direct <c>RagDbContext</c> SQL.
/// Offline invariant: pure Microsoft.Data.Sqlite, no Ollama/LanceDB/HTTP.
/// </summary>
public interface IQueryRepository
{
    /// <summary>
    /// Lists the caller's own queries (UserId == sub, legacy excluded),
    /// ordered CreatedAt DESC, Id DESC.
    /// </summary>
    Task<HistoryPage> ListHistoryAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the full prompt/answer plus citations for one owned query,
    /// or null when not found / not owned / legacy.
    /// </summary>
    Task<QueryDetail?> GetDetailAsync(
        string sub,
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Persists a query record (the single write channel for Queries).
    /// </summary>
    Task AddAsync(Query query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latency (ms) of the most recent 100 queries for health reporting.
    /// </summary>
    Task<IReadOnlyList<int>> GetRecentLatenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs EXPLAIN QUERY PLAN for the history list query.
    /// </summary>
    Task<IReadOnlyList<string>> GetExplainPlanAsync(
        string sub,
        int limit,
        int offset,
        CancellationToken cancellationToken = default
    );
}
