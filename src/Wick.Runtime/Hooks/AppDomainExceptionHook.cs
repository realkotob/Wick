using System;
using System.Threading;

namespace Wick.Runtime.Hooks;

/// <summary>
/// Registers <see cref="AppDomain.UnhandledException"/>. Note that Godot 4.6.1 does NOT
/// currently route engine-callback exceptions through this event (see godot#73515) — this
/// hook is registered for forward-compatibility with the fix, and to capture any threads
/// that do NOT run through a Godot engine callback.
/// </summary>
public sealed class AppDomainExceptionHook
{
    private UnhandledExceptionEventHandler? _handler;
    private int _installed;

    public void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }
        _handler = OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += _handler;
    }

    public void Uninstall()
    {
        if (Interlocked.Exchange(ref _installed, 0) == 0)
        {
            return;
        }
        if (_handler is not null)
        {
            AppDomain.CurrentDomain.UnhandledException -= _handler;
            _handler = null;
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var ex = e.ExceptionObject as Exception
                ?? new InvalidOperationException($"UnhandledException with non-Exception payload: {e.ExceptionObject}");
            var payload = StackFrameParser.ToPayload(ex);
            WickEnvelope.WriteEnvelope("exception", payload);
        }
        catch
        {
            // We are on the terminal path — never throw from the terminal path.
        }
    }
}
