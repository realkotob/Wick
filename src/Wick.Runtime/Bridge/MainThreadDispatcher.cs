using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Wick.Runtime.Bridge;

/// <summary>
/// Queues actions for the Godot main thread. The user MUST call <see cref="Tick"/> from a
/// main-thread context once per frame (typically from their autoload's <c>_Process(delta)</c>)
/// for queued work to run. If Godot 4.x ever exposes a first-class "post to main thread"
/// primitive from C# land, this can disappear — until then, cooperative ticking is the
/// portable answer.
/// </summary>
public sealed class MainThreadDispatcher
{
    private static readonly Lazy<MainThreadDispatcher> s_instance = new(() => new MainThreadDispatcher());
    public static MainThreadDispatcher Instance => s_instance.Value;

    private readonly ConcurrentQueue<Action> _queue = new();

    private MainThreadDispatcher() { }

    /// <summary>
    /// Queues <paramref name="action"/> for main-thread execution. Fire-and-forget; use
    /// <see cref="Run{T}"/> if you need the result back.
    /// </summary>
    public void Schedule(Action action) => _queue.Enqueue(action);

    /// <summary>
    /// Drains all queued actions. Call from the main thread exactly once per frame.
    /// Exceptions from individual actions are swallowed so a bad RPC cannot wedge the game.
    /// </summary>
    public void Tick()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch
            {
                // Intentionally swallowed; RPC callers report via the response channel.
            }
        }
    }

    /// <summary>
    /// Schedules <paramref name="work"/> on the main thread and blocks the calling (bridge
    /// worker) thread until it completes or <paramref name="timeout"/> elapses. Returns the
    /// result. Throws the original exception if <paramref name="work"/> fails.
    /// </summary>
    public T Run<T>(Func<T> work, TimeSpan timeout)
    {
        var done = new ManualResetEventSlim(false);
        T result = default!;
        Exception? failure = null;

        Schedule(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });

        if (!done.Wait(timeout))
        {
            throw new TimeoutException(
                $"Main-thread dispatch did not complete within {timeout.TotalMilliseconds}ms. " +
                "Ensure WickRuntime.Tick() is being called from the main thread (autoload _Process).");
        }
        if (failure is not null)
        {
            throw failure;
        }
        return result;
    }

    /// <summary>Queue size for tests/diagnostics.</summary>
    public int PendingCount => _queue.Count;
}
