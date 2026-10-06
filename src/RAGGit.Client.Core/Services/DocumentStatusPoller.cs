using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAGGit.Client.Core.Services;

/// <summary>
/// Drives the periodic "refetch the list while any row is still processing"
/// loop shared by the Library and My Documents tables. Owns the interval and
/// the lifetime token so a page can stop polling when it is unloaded; callers
/// supply the "keep going?" predicate and the single-tick fetch/apply.
/// </summary>
/// <remarks>
/// Deliberately does not use <c>ConfigureAwait(false)</c>: the loop is started
/// from the UI thread, so continuations must resume there to mutate bound
/// collections safely. Cancellation usually surfaces as a mapped
/// <c>HttpRequestException</c> (the API clients translate the timeout), so the
/// loop treats a cancelled token as a normal exit rather than an error.
/// </remarks>
public sealed class DocumentStatusPoller : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;

    public DocumentStatusPoller(TimeSpan interval)
    {
        _interval = interval;
    }

    /// <summary>True while a polling loop is running.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _cts is { IsCancellationRequested: false };
            }
        }
    }

    /// <summary>
    /// Starts polling when <paramref name="keepPolling"/> currently holds and no
    /// loop is already running. Idempotent; safe to call on every load.
    /// </summary>
    /// <param name="keepPolling">Re-evaluated before each tick; false ends the loop.</param>
    /// <param name="pollOnce">One fetch-and-apply pass.</param>
    /// <param name="onError">Reports a non-cancellation failure; the loop then ends.</param>
    public void Start(
        Func<bool> keepPolling,
        Func<CancellationToken, Task> pollOnce,
        Action<Exception>? onError = null
    )
    {
        ArgumentNullException.ThrowIfNull(keepPolling);
        ArgumentNullException.ThrowIfNull(pollOnce);

        lock (_gate)
        {
            if (_cts is { IsCancellationRequested: false })
            {
                return;
            }

            if (!keepPolling())
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _cts = cts;
            _ = RunAsync(keepPolling, pollOnce, onError, cts);
        }
    }

    private async Task RunAsync(
        Func<bool> keepPolling,
        Func<CancellationToken, Task> pollOnce,
        Action<Exception>? onError,
        CancellationTokenSource cts
    )
    {
        var token = cts.Token;
        try
        {
            while (!token.IsCancellationRequested && keepPolling())
            {
                await Task.Delay(_interval, token);

                if (token.IsCancellationRequested)
                {
                    break;
                }

                await pollOnce(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Stop() while a delay/fetch was in flight is the normal exit.
        }
        catch (Exception exception)
        {
            // A cancelled request is mapped to HttpRequestException by the API
            // clients; never report that as a refresh failure.
            if (!token.IsCancellationRequested)
            {
                onError?.Invoke(exception);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                }
            }

            cts.Dispose();
        }
    }

    /// <summary>Cancels the active loop, if any. Safe to call when idle.</summary>
    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The loop already finished and disposed its token source.
        }
    }

    public void Dispose() => Stop();
}
