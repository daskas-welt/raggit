using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using RAGGit.Client.WPF;

namespace RAGGit.Tests.Client;

/// <summary>
/// Hosts the real <see cref="App"/> for the lifetime of a test collection.
/// WPF requires a single <see cref="Application"/> per AppDomain and an STA
/// thread, so every screen check marshals onto one long-lived dispatcher
/// instead of constructing its own.
/// </summary>
public sealed class WpfAppFixture : IDisposable
{
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;
    private readonly ExceptionDispatchInfo? _startupFailure;

    public WpfAppFixture()
    {
        using var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;
        ExceptionDispatchInfo? failure = null;

        _thread = new Thread(() =>
        {
            try
            {
                // Screens are loaded through assembly-qualified pack URIs, so the
                // host does not have to impersonate the entry assembly.
                var application = new App();
                // Loads Application.Resources: the WPF-UI theme/control
                // dictionaries, the design-system styles and spacing tokens.
                application.InitializeComponent();

                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                ready.Set();
            }

            if (failure is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "raggit-wpf-test-host",
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();

        _startupFailure = failure;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the WPF dispatcher thread, rethrowing
    /// the original exception so a failing screen reports its real cause.
    /// </summary>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _startupFailure?.Throw();

        try
        {
            _dispatcher.Invoke(action);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    /// <summary>Runs a function on the dispatcher thread and returns its result.</summary>
    public T Run<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        _startupFailure?.Throw();

        try
        {
            return _dispatcher.Invoke(func);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public void Dispose()
    {
        if (_startupFailure is null)
        {
            _dispatcher.InvokeShutdown();
        }

        _thread.Join(TimeSpan.FromSeconds(10));
    }
}

/// <summary>
/// One shared WPF host. Parallelization is disabled because WPF's
/// <see cref="Application"/> is a per-AppDomain singleton and screens touch
/// shared dispatcher state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfCollection : ICollectionFixture<WpfAppFixture>
{
    public const string Name = "wpf-host";
}
