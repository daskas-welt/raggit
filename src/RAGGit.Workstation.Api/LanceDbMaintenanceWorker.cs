using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Models;
using RAGGit.Ingest.Vector;

namespace RAGGit.Workstation.Api;

/// <summary>
/// After ingest drains, compacts the LanceDB collection and (when the table is
/// large enough) builds the vector index. LanceDB is append-only, so every
/// ingest/delete/re-index adds fragments; without this pass retrieval degrades
/// and the collection grows unbounded. Runs off the request path, serialized
/// against writes by the client's own lock.
/// </summary>
public sealed class LanceDbMaintenanceWorker : BackgroundService
{
    private readonly IVectorStore _vectorStore;
    private readonly IngestWorkQueue _queue;
    private readonly IngestOptions _options;
    private readonly ILogger<LanceDbMaintenanceWorker> _logger;

    public LanceDbMaintenanceWorker(
        IVectorStore vectorStore,
        IngestWorkQueue queue,
        IOptions<IngestOptions> options,
        ILogger<LanceDbMaintenanceWorker> logger
    )
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _options = options?.Value ?? new IngestOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Maintenance is LanceDB-specific; other stores are a no-op.
        if (_vectorStore is not LanceDbLocalClient lance)
        {
            _logger.LogInformation("Vector store is not LanceDB; maintenance worker is disabled.");
            return;
        }

        var interval =
            _options.MaintenanceInterval > TimeSpan.Zero
                ? _options.MaintenanceInterval
                : TimeSpan.FromSeconds(30);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, stoppingToken);

                // Only after ingest has drained. Short-circuit on outstanding
                // so the dirty flag survives until the queue is empty.
                if (_queue.OutstandingCount > 0 || !_queue.ConsumeDirty())
                {
                    continue;
                }

                await RunMaintenanceAsync(lance, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunMaintenanceAsync(
        LanceDbLocalClient lance,
        CancellationToken cancellationToken
    )
    {
        // Two independent steps: a compaction failure must not block the index
        // build, and vice versa. Dirty is only re-armed by a new enqueue, so a
        // failure retries on the next ingest rather than on every tick.
        try
        {
            var stats = await lance.OptimizeAsync(
                _options.MaintenanceCleanupOlderThan,
                cancellationToken
            );
            if (stats.Compaction is { } compaction)
            {
                _logger.LogInformation(
                    "LanceDB compaction: {Removed} fragment(s) removed, {Added} added",
                    compaction.FilesRemoved,
                    compaction.FilesAdded
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "LanceDB compaction failed");
        }

        try
        {
            var indexed = await lance.EnsureVectorIndexAsync(
                _options.VectorIndexThresholdRows,
                cancellationToken
            );
            _logger.LogInformation(
                "LanceDB vector index {State}",
                indexed ? "present" : "skipped (below row threshold)"
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "LanceDB index build failed");
        }
    }
}
