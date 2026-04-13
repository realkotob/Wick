using System.Text.Json;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Recognizes and parses <c>Wick.Runtime</c> stderr envelopes. Envelope lines always start
/// with the literal prefix <c>{"__wick":1</c>, which makes them trivially distinguishable
/// from raw Godot output. Non-envelope lines are passed through unchanged for the legacy
/// <see cref="GodotExceptionParser"/> path.
/// </summary>
public static class WickEnvelopeParser
{
    /// <summary>The well-known prefix every Wick envelope line starts with.</summary>
    public const string Marker = "{\"__wick\":1";

    /// <summary>Returns true if <paramref name="line"/> looks like a Wick envelope.</summary>
    public static bool IsEnvelope(string line)
        => !string.IsNullOrEmpty(line) && line.TrimStart().StartsWith(Marker, System.StringComparison.Ordinal);

    /// <summary>
    /// Parses a Wick envelope line. Returns null if parsing fails (so the caller can fall
    /// through to the raw Godot parser rather than dropping the line entirely).
    /// </summary>
    public static WickEnvelopeEvent? Parse(string line)
    {
        if (!IsEnvelope(line))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var kind = root.TryGetProperty("kind", out var kEl) && kEl.ValueKind == JsonValueKind.String
                ? kEl.GetString()
                : null;
            if (string.IsNullOrEmpty(kind))
            {
                return null;
            }

            var payload = root.TryGetProperty("payload", out var pEl) ? (JsonElement?)pEl.Clone() : null;
            return new WickEnvelopeEvent(kind, payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Projects an <c>"exception"</c> envelope payload into a <see cref="RawException"/>.
    /// Returns null if required fields are missing.
    /// </summary>
    public static RawException? TryProjectException(JsonElement payload, string rawLine)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = ReadString(payload, "type");
        var message = ReadString(payload, "message");
        if (type is null || message is null)
        {
            return null;
        }

        var frames = new List<ExceptionFrame>();
        if (payload.TryGetProperty("frames", out var framesEl) && framesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in framesEl.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var method = ReadString(f, "method") ?? "<unknown>";
                var filePath = ReadString(f, "file_path");
                int? line = null;
                if (f.TryGetProperty("line", out var lineEl) && lineEl.ValueKind == JsonValueKind.Number && lineEl.TryGetInt32(out var li))
                {
                    line = li;
                }
                var isUserCode = f.TryGetProperty("is_user_code", out var uEl) && uEl.ValueKind == JsonValueKind.True;
                frames.Add(new ExceptionFrame(method, filePath, line, isUserCode));
            }
        }

        return new RawException
        {
            Type = type,
            Message = message,
            RawText = rawLine,
            Frames = frames,
        };
    }

    /// <summary>Projects a <c>"log"</c> envelope payload into a display-friendly log line.</summary>
    public static string? TryProjectLogLine(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var level = ReadString(payload, "level") ?? "Information";
        var category = ReadString(payload, "category") ?? "<unknown>";
        var message = ReadString(payload, "message") ?? string.Empty;
        return $"[wick] [{level}] {category}: {message}";
    }

    /// <summary>
    /// Extracts the port from a <c>"handshake"</c> envelope payload. Returns null on failure.
    /// </summary>
    public static int? TryProjectHandshakePort(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("port", out var pEl))
        {
            return null;
        }
        return pEl.ValueKind == JsonValueKind.Number && pEl.TryGetInt32(out var p) ? p : null;
    }

    private static string? ReadString(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            return el.GetString();
        }
        return null;
    }
}

/// <summary>A parsed Wick envelope: <see cref="Kind"/> determines how to read <see cref="Payload"/>.</summary>
public sealed record WickEnvelopeEvent(string Kind, JsonElement? Payload);
