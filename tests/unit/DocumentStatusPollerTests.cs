using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Services;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// The shared poll loop behind the Library and My Documents status refresh:
/// periodic ticks, stop-on-terminal, prompt cancellation, single loop.
/// </summary>
public sealed class DocumentStatusPollerTests
{
    [Fact]
    public async Task Start_WhenPredicateHolds_PollsUntilPredicateFalse()
    {
        var polls = 0;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(
            keepPolling: () => Volatile.Read(ref polls) < 3,
            pollOnce: _ =>
            {
                Interlocked.Increment(ref polls);
                return Task.CompletedTask;
            }
        );

        await WaitUntilAsync(() => Volatile.Read(ref polls) >= 3);
        await WaitUntilAsync(() => !poller.IsRunning);

        Volatile.Read(ref polls).Should().Be(3);
    }

    [Fact]
    public void Start_WhenPredicateFalse_DoesNotPoll()
    {
        var polls = 0;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(
            keepPolling: () => false,
            pollOnce: _ =>
            {
                Interlocked.Increment(ref polls);
                return Task.CompletedTask;
            }
        );

        poller.IsRunning.Should().BeFalse();
        Volatile.Read(ref polls).Should().Be(0);
    }

    [Fact]
    public async Task Start_IsIdempotent_OnlyOneLoopRuns()
    {
        var concurrent = 0;
        var peak = 0;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        async Task PollAsync(CancellationToken _)
        {
            var now = Interlocked.Increment(ref concurrent);
            lock (poller)
            {
                if (now > peak)
                {
                    peak = now;
                }
            }

            await Task.Delay(20);
            Interlocked.Decrement(ref concurrent);
        }

        poller.Start(() => true, PollAsync);
        poller.Start(() => true, PollAsync);

        await Task.Delay(150);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning);

        Volatile.Read(ref peak).Should().Be(1);
    }

    [Fact]
    public async Task Stop_CancelsLoopAndNoFurtherTicks()
    {
        var polls = 0;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(
            keepPolling: () => true,
            pollOnce: _ =>
            {
                Interlocked.Increment(ref polls);
                return Task.CompletedTask;
            }
        );

        await WaitUntilAsync(() => Volatile.Read(ref polls) >= 1);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning);

        var settled = Volatile.Read(ref polls);
        await Task.Delay(60);
        Volatile.Read(ref polls).Should().Be(settled);
    }

    [Fact]
    public async Task PollError_ReportsAndStops()
    {
        var errors = 0;
        Exception? captured = null;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(
            keepPolling: () => true,
            pollOnce: _ => throw new InvalidOperationException("boom"),
            onError: exception =>
            {
                captured = exception;
                Interlocked.Increment(ref errors);
            }
        );

        await WaitUntilAsync(() => Volatile.Read(ref errors) == 1);
        await WaitUntilAsync(() => !poller.IsRunning);

        captured.Should().BeOfType<InvalidOperationException>();
        Volatile.Read(ref errors).Should().Be(1);
    }

    [Fact]
    public async Task StopDuringPoll_IsNotReportedAsError()
    {
        var errors = 0;
        var poller = new DocumentStatusPoller(TimeSpan.FromMilliseconds(10));

        poller.Start(
            keepPolling: () => true,
            pollOnce: token => Task.Delay(Timeout.Infinite, token),
            onError: _ => Interlocked.Increment(ref errors)
        );

        await WaitUntilAsync(() => poller.IsRunning);
        await Task.Delay(30);
        poller.Stop();
        await WaitUntilAsync(() => !poller.IsRunning);

        Volatile.Read(ref errors).Should().Be(0);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(10);
        }
    }
}
