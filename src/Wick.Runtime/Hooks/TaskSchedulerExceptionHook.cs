using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wick.Runtime.Hooks;

/// <summary>
/// Registers <see cref="TaskScheduler.UnobservedTaskException"/> and forwards any captured
/// exception through the Wick stderr envelope transport. The exception is marked observed
/// after capture so the process does not crash purely on account of Wick seeing it; the
/// user's own diagnostics (logging, crash reporter) continue to run as normal.
/// </summary>
public sealed class TaskSchedulerExceptionHook
{
    private EventHandler<UnobservedTaskExceptionEventArgs>? _handler;
    private int _installed;

    /// <summary>
    /// Installs the hook. Idempotent: a second call is a no-op.
    /// </summary>
    public void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }
        _handler = OnUnobservedTaskException;
        TaskScheduler.UnobservedTaskException += _handler;
    }

    /// <summary>
    /// Removes the hook if installed. Idempotent.
    /// </summary>
    public void Uninstall()
    {
        if (Interlocked.Exchange(ref _installed, 0) == 0)
        {
            return;
        }
        if (_handler is not null)
        {
            TaskScheduler.UnobservedTaskException -= _handler;
            _handler = null;
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var ex = (Exception?)e.Exception ?? new InvalidOperationException("UnobservedTaskException with no inner");
            var payload = StackFrameParser.ToPayload(ex);
            WickEnvelope.WriteEnvelope("exception", payload);
        }
        catch
        {
            // Never let our capture path itself tear the process down.
        }
        finally
        {
            // Mark observed so the default finalizer-thread rethrow does not fire.
            // Users who WANT fast-fail semantics can opt out of this hook via options.
            try { e.SetObserved(); } catch { /* already observed */ }
        }
    }
}
