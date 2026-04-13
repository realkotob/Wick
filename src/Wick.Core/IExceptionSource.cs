namespace Wick.Core;

/// <summary>
/// Pluggable source of raw exceptions from a running Godot game.
/// Implementations: BridgeExceptionSource (primary), LogFileExceptionSource (fallback).
/// When Godot fixes AppDomain.UnhandledException (godot#73515),
/// AppDomainExceptionSource plugs in alongside existing sources.
/// </summary>
public interface IExceptionSource
{
    IAsyncEnumerable<RawException> CaptureAsync(CancellationToken ct);
}
