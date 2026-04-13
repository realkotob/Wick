namespace Wick.Core;

/// <summary>
/// A single frame from a .NET stack trace, parsed from Godot's error output.
/// </summary>
public sealed record ExceptionFrame(
    string Method,
    string? FilePath,
    int? Line,
    bool IsUserCode);

/// <summary>
/// A parsed but un-enriched exception from Godot's error output.
/// Contains the raw structural data extracted by the parser.
/// </summary>
public sealed class RawException
{
    public required string Type { get; init; }
    public required string Message { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string RawText { get; init; }
    public IReadOnlyList<ExceptionFrame> Frames { get; init; } = [];
}

/// <summary>
/// Source-level context for the throwing stack frame, produced by the Roslyn enricher.
/// </summary>
public sealed class SourceContext
{
    public string? MethodBody { get; init; }
    public string? SurroundingLines { get; init; }
    public string? EnclosingType { get; init; }
    public IReadOnlyList<string> Callers { get; init; } = [];
    public string? NearestComment { get; init; }
}

/// <summary>
/// Scene-tree context at the moment of the exception, from the Godot bridge.
/// </summary>
public sealed class SceneContext
{
    public string? ScenePath { get; init; }
    public string? ThrowingNode { get; init; }
    public int? NodeCount { get; init; }
}

/// <summary>
/// A fully enriched exception: raw parse + Roslyn source context + logs + scene state.
/// </summary>
public sealed class EnrichedException
{
    public required RawException Raw { get; init; }
    public SourceContext? Source { get; init; }
    public IReadOnlyList<string> RecentLogs { get; init; } = [];
    public SceneContext? Scene { get; init; }
}
