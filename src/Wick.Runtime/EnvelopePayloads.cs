using System.Collections.Generic;

namespace Wick.Runtime;

/// <summary>
/// Wire-shape of an exception payload inside a Wick envelope. Kept deliberately separate
/// from <c>Wick.Core.RawException</c> so <c>Wick.Runtime</c> has zero references to
/// <c>Wick.Core</c> and can ship as an independently versioned NuGet.
/// </summary>
public sealed record ExceptionPayload(
    string Type,
    string Message,
    string? StackTrace,
    IReadOnlyList<FramePayload> Frames);

/// <summary>
/// Wire-shape of a single stack frame. Mirrors <c>Wick.Core.ExceptionFrame</c>.
/// </summary>
public sealed record FramePayload(
    string Method,
    string? FilePath,
    int? Line,
    bool IsUserCode);

/// <summary>
/// Wire-shape of a log event written via <see cref="Logging.WickLogger"/>.
/// </summary>
public sealed record LogPayload(
    string Category,
    string Level,
    string Message,
    ExceptionPayload? Exception);

/// <summary>
/// Wire-shape of the startup handshake, announcing the bridge server's port and version.
/// </summary>
public sealed record HandshakePayload(
    int Port,
    string Version);
