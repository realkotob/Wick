using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Wick.Runtime;

/// <summary>
/// Writes Wick stderr envelopes: single-line JSON objects of the shape
/// <c>{"__wick":1,"kind":"&lt;kind&gt;","timestamp":"&lt;iso8601&gt;","payload":&lt;obj&gt;}</c>.
///
/// The Wick server (ProcessExceptionSource + WickEnvelopeParser) recognizes these envelopes
/// and routes them into the exception pipeline / log buffer, while non-envelope stderr lines
/// continue to flow through the legacy GodotExceptionParser.
///
/// All writes are serialized through a single lock so concurrent exceptions, logs, and
/// handshake writes never interleave on stderr.
/// </summary>
public static class WickEnvelope
{
    /// <summary>The well-known prefix every Wick envelope line starts with.</summary>
    public const string Marker = "{\"__wick\":1";

    private static readonly object s_lock = new();

    /// <summary>Serializer options used for envelope payloads. Exposed for parser reuse.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    /// <summary>
    /// Serializes <paramref name="payload"/> into a Wick envelope and writes exactly one
    /// line to <see cref="Console.Error"/>. Thread-safe.
    /// </summary>
    public static void WriteEnvelope(string kind, object payload)
    {
        var line = Format(kind, payload);
        lock (s_lock)
        {
            Console.Error.WriteLine(line);
        }
    }

    /// <summary>
    /// Returns a formatted handshake envelope line (does not write it).
    /// </summary>
    public static string FormatHandshake(int port, string version)
    {
        var payload = new Dictionary<string, object?>
        {
            ["port"] = port,
            ["version"] = version,
        };
        return Format("handshake", payload);
    }

    /// <summary>
    /// Serializes an envelope to its wire string without writing it. Exposed for testing.
    /// </summary>
    public static string Format(string kind, object payload)
    {
        var envelope = new Dictionary<string, object?>
        {
            ["__wick"] = 1,
            ["kind"] = kind,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            ["payload"] = payload,
        };
        return JsonSerializer.Serialize(envelope, Options);
    }
}
