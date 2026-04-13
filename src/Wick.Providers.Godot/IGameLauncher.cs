namespace Wick.Providers.Godot;

/// <summary>
/// Abstraction over the actual subprocess spawn so RuntimeTools can be unit-tested
/// without forking real Godot processes.
/// </summary>
public interface IGameLauncher
{
    /// <summary>
    /// Starts a Godot game process and returns its pid. The implementation is
    /// responsible for wiring the process's stderr into the exception pipeline.
    /// The returned <see cref="LaunchedGame.OnStop"/> callback, when invoked, must kill
    /// the process and drain remaining output.
    /// </summary>
    LaunchedGame Launch(string? scene, bool headless, IReadOnlyList<string> extraArgs);
}

public sealed record LaunchedGame(int Pid, DateTimeOffset StartedAt, Action OnStop);
