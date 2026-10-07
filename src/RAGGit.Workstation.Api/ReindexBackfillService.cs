using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAGGit.Core.Abstractions.Repositories;

namespace RAGGit.Workstation.Api;

/// <summary>
/// One-shot upgrade for pre-feature libraries (030, research R5): after the
/// schema exists, finds <c>Ready</c> documents with no parent chunk row and
/// re-processes each through the existing background path from its stored
/// original (re-chunk → re-embed → upsert → persist). Documents that are
/// already two-level are skipped, so the pass is idempotent. Work is
/// published through <see cref="IngestWorkQueue"/> so the ingest workers
/// (and the maintenance pass afterwards) handle it like any upload.
/// </summary>
public sealed class ReindexBackfillService : BackgroundService
{
    private readonly IDocumentRepository _documents;
    private readonly IngestWorkQueue _queue;
    private readonly ILogger<ReindexBackfillService> _logger;

    public ReindexBackfillService(
        IDocumentRepository documents,
        IngestWorkQueue queue,
        ILogger<ReindexBackfillService> logger
    )
    {
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await BackfillAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // Backfill must never crash startup: stale documents simply stay
            // single-level until the next restart re-runs this pass.
            _logger.LogError(exception, "Two-level backfill failed; will retry on next startup");
        }
    }

    /// <summary>
    /// Re-enqueues every stale <c>Ready</c> document. Returns how many were
    /// queued (0 when the library is already two-level).
    /// </summary>
    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var stale = await _documents.ListReadyDocumentIdsWithoutParentChunkAsync(cancellationToken);

        foreach (var documentId in stale)
        {
            await _queue.EnqueueAsync(documentId, cancellationToken);
        }

        if (stale.Count > 0)
        {
            _logger.LogInformation(
                "Two-level backfill re-queued {Count} stale documents",
                stale.Count
            );
        }

        return stale.Count;
    }
}
