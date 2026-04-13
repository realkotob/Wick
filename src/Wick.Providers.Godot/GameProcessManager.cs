namespace Wick.Providers.Godot;

/// <summary>
/// Single-process lifecycle owner for agent-launched Godot games.
/// Exactly one game can be running at a time; a second launch attempt fails.
/// </summary>
public sealed class GameProcessManager
{
    private readonly object _gate = new();
    private LaunchState? _state;

    public GameStatusDto Status
    {
        get
        {
            lock (_gate)
            {
                return _state is null
                    ? new GameStatusDto(false, null, null)
                    : new GameStatusDto(true, _state.Pid, _state.StartedAt);
            }
        }
    }

    /// <summary>
    /// Attempts to register a newly-launched game. Returns false if a game is already running.
    /// </summary>
    public bool TryLaunch(int pid, DateTimeOffset startedAt, Action onStop)
    {
        lock (_gate)
        {
            if (_state is not null)
            {
                return false;
            }
            _state = new LaunchState(pid, startedAt, onStop);
            return true;
        }
    }

    /// <summary>
    /// Stops the current game if one is running. Invokes the onStop callback to
    /// let the caller actually kill the process and drain its streams.
    /// </summary>
    public StopResultDto Stop(int? exitCode)
    {
        LaunchState? captured;
        lock (_gate)
        {
            captured = _state;
            _state = null;
        }

        if (captured is null)
        {
            return new StopResultDto(false, null, 0);
        }

        captured.OnStop();
        var duration = (long)(DateTimeOffset.UtcNow - captured.StartedAt).TotalMilliseconds;
        return new StopResultDto(true, exitCode, duration);
    }

    private sealed record LaunchState(int Pid, DateTimeOffset StartedAt, Action OnStop);
}

public sealed record GameStatusDto(bool IsRunning, int? Pid, DateTimeOffset? StartedAt);
public sealed record StopResultDto(bool Stopped, int? ExitCode, long DurationMs);
