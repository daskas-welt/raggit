using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAGGit.Core.Models;
using RAGGit.Ingest;

namespace RAGGit.Workstation.Api;

public sealed class IngestWorkQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly IngestService _ingestService;
    private readonly ILogger<IngestWorkQueue> _logger;
    private int _outstanding;
    private int _dirty;

    public IngestWorkQueue(IngestService ingestService, ILogger<IngestWorkQueue> logger)
    {
        _ingestService = ingestService ?? throw new ArgumentNullException(nameof(ingestService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Marks the document as queued and then publishes it to the workers.
    /// The order matters: a worker can finish a document the moment it is
    /// published, so writing the queued status afterwards could overwrite a
    /// newer state. The status write is best-effort — a database hiccup must
    /// never drop the work, and the startup reconciliation re-queues anything
    /// still stuck on <see cref="DocumentStatus.Uploading"/>.
    /// </summary>
    public async ValueTask EnqueueAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _ingestService.MarkQueuedAsync(documentId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not mark document {DocumentId} as queued before publishing it",
                documentId
            );
        }

        await _queue.Writer.WriteAsync(documentId, cancellationToken);
        Interlocked.Increment(ref _outstanding);
        MarkDirty();
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Documents published but not yet finished (success or failure). Zero
    /// means ingest has drained — the trigger for maintenance.
    /// </summary>
    public int OutstandingCount => Volatile.Read(ref _outstanding);

    /// <summary>Called by a worker once a document is settled.</summary>
    public void MarkProcessed() => Interlocked.Decrement(ref _outstanding);

    /// <summary>
    /// Flags that ingest happened since the last maintenance pass. Set on
    /// enqueue and again on maintenance failure so the work is not lost.
    /// </summary>
    public void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    /// <summary>
    /// Returns whether ingest happened since the last call, clearing the flag.
    /// The maintenance worker consumes this only when the queue has drained.
    /// </summary>
    public bool ConsumeDirty() => Interlocked.Exchange(ref _dirty, 0) == 1;
}

public sealed class IngestWorker : BackgroundService
{
    private readonly IngestWorkQueue _queue;
    private readonly IngestService _ingestService;
    private readonly ILogger<IngestWorker> _logger;
    private readonly IngestOptions _options;

    public IngestWorker(
        IngestWorkQueue queue,
        IngestService ingestService,
        ILogger<IngestWorker> logger,
        IOptions<IngestOptions> options
    )
    {
        _queue = queue;
        _ingestService = ingestService;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var documents = await _ingestService.ListDocumentsAsync(stoppingToken);
            foreach (var document in documents)
            {
                if (
                    document.Status
                    is DocumentStatus.Uploading
                        or DocumentStatus.Queued
                        or DocumentStatus.Indexing
                )
                {
                    await _queue.EnqueueAsync(document.Id, stoppingToken);
                }
            }

            var workerCount = Math.Max(1, _options.MaxConcurrentJobs);
            var workers = Enumerable
                .Range(0, workerCount)
                .Select(_ => ConsumeAsync(stoppingToken))
                .ToArray();
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Document ingest worker stopped unexpectedly");
            throw;
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await foreach (var documentId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _ingestService.ProcessStagedAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Document ingest worker failed for {DocumentId}",
                    documentId
                );
            }
            finally
            {
                _queue.MarkProcessed();
            }
        }
    }
}
