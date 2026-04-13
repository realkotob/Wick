using System.Text.RegularExpressions;

namespace Wick.Core;

/// <summary>
/// Parses Godot Engine's stderr exception output into structured <see cref="RawException"/> objects.
/// Godot 4.6.x swallows AppDomain.UnhandledException and logs exceptions to stderr in a two-section
/// format: Section 1 is a standard .NET stack trace, Section 2 is a "C# backtrace" block (ignored).
/// </summary>
public static partial class GodotExceptionParser
{
    // Match: ERROR: Namespace.Type: message
    // The type must contain at least one dot to distinguish from Godot resource errors like
    // "ERROR: res://scenes/Level.tscn: Resource not found"
    // Type must look like a .NET namespace-qualified type: starts with a letter, contains at least one dot,
    // and consists of word chars and dots. This excludes Godot resource paths like "res://scenes/Level.tscn".
    [GeneratedRegex(@"^ERROR:\s+([A-Za-z][\w]*(?:`\d+|[.+][A-Za-z][\w]*(?:`\d+)?)+):\s+(.+)$", RegexOptions.Compiled)]
    private static partial Regex ExceptionHeaderRegex();

    // Match standard .NET "   at Method(...) in filepath:line N" frames
    [GeneratedRegex(@"^\s+at\s+(.+?)(?:\s+in\s+(.+?):line\s+(\d+))?\s*$", RegexOptions.Compiled)]
    private static partial Regex StandardFrameRegex();

    // Match Godot's "   at: void Namespace.Method(...) (filepath:line)" format
    [GeneratedRegex(@"^\s+at:\s+\S+\s+(.+?)\s+\((.+?):(\d+)\)\s*$", RegexOptions.Compiled)]
    private static partial Regex GodotFrameRegex();

    // Match mono-style "[0x00000] in <guid>:0" — extract the method before "in"
    [GeneratedRegex(@"^\s+at\s+(.+?)\s+in\s+\[0x[0-9a-fA-F]+\]\s+in\s+<.+?>:(\d+)\s*$", RegexOptions.Compiled)]
    private static partial Regex MonoFrameRegex();

    private static readonly string[] InternalPrefixes =
    [
        "Godot.Bridge.",
        "Godot.NativeInterop.",
        "Godot.Node.InvokeGodotClassMethod",
        "Godot.GD.Push",
    ];

    /// <summary>
    /// Parse a single exception block from Godot's stderr output.
    /// Returns null if the input is null, empty, or not a parseable exception.
    /// Never throws.
    /// </summary>
    public static RawException? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        try
        {
            return ParseCore(input);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or RegexMatchTimeoutException)
        {
            // Malformed input — return null rather than crashing the pipeline
            return null;
        }
    }

    /// <summary>
    /// Parse mixed output that may contain multiple exceptions interleaved with log lines.
    /// Returns an empty list if no exceptions are found. Never throws.
    /// </summary>
    public static IReadOnlyList<RawException> ParseStream(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        try
        {
            return ParseStreamCore(input);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or RegexMatchTimeoutException)
        {
            // Malformed input — return empty list rather than crashing the pipeline
            return [];
        }
    }

    private static RawException? ParseCore(string input)
    {
        var lines = input.Split('\n');

        string? type = null;
        string? message = null;
        var frames = new List<ExceptionFrame>();

        foreach (var line in lines)
        {
            // Stop at Section 2
            if (line.TrimStart().StartsWith("C# backtrace", StringComparison.Ordinal))
            {
                break;
            }

            // Try to match exception header
            if (type is null)
            {
                var headerMatch = ExceptionHeaderRegex().Match(line);
                if (headerMatch.Success)
                {
                    type = headerMatch.Groups[1].Value;
                    message = headerMatch.Groups[2].Value;
                    continue;
                }

                // Keep looking for the header
                continue;
            }

            // Try to parse stack frames (only after header is found)
            var frame = TryParseFrame(line);
            if (frame is not null)
            {
                frames.Add(frame);
            }
        }

        if (type is null || message is null)
        {
            return null;
        }

        return new RawException
        {
            Type = type,
            Message = message,
            RawText = input,
            Frames = frames,
        };
    }

    private static List<RawException> ParseStreamCore(string input)
    {
        var results = new List<RawException>();
        var lines = input.Split('\n');
        var blockLines = new List<string>();
        var inExceptionBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (ExceptionHeaderRegex().IsMatch(line))
            {
                // Flush previous block
                if (inExceptionBlock && blockLines.Count > 0)
                {
                    var parsed = ParseCore(string.Join('\n', blockLines));
                    if (parsed is not null)
                    {
                        results.Add(parsed);
                    }
                }

                blockLines = [line];
                inExceptionBlock = true;
                continue;
            }

            if (inExceptionBlock)
            {
                // If this line looks like a stack frame, inner exception, or C# backtrace, include it
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("at ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("at:", StringComparison.Ordinal) ||
                    trimmed.StartsWith("C# backtrace", StringComparison.Ordinal) ||
                    trimmed.StartsWith('[') ||
                    trimmed.StartsWith("---", StringComparison.Ordinal) ||
                    trimmed.StartsWith("--->", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(line))
                {
                    blockLines.Add(line);
                }
                else
                {
                    // Non-frame line — flush block
                    var parsed = ParseCore(string.Join('\n', blockLines));
                    if (parsed is not null)
                    {
                        results.Add(parsed);
                    }

                    blockLines.Clear();
                    inExceptionBlock = false;
                }
            }
        }

        // Flush final block
        if (inExceptionBlock && blockLines.Count > 0)
        {
            var parsed = ParseCore(string.Join('\n', blockLines));
            if (parsed is not null)
            {
                results.Add(parsed);
            }
        }

        return results;
    }

    private static ExceptionFrame? TryParseFrame(string line)
    {
        // Try mono format first (more specific — would otherwise partially match the standard regex)
        var match = MonoFrameRegex().Match(line);
        if (match.Success)
        {
            var lineNum = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return CreateFrame(
                match.Groups[1].Value,
                null,
                lineNum == 0 ? null : lineNum);
        }

        // Try standard .NET frame format
        match = StandardFrameRegex().Match(line);
        if (match.Success)
        {
            return CreateFrame(
                match.Groups[1].Value,
                match.Groups[2].Success ? match.Groups[2].Value : null,
                match.Groups[3].Success ? int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : null);
        }

        // Try Godot's "at:" format
        match = GodotFrameRegex().Match(line);
        if (match.Success)
        {
            return CreateFrame(
                match.Groups[1].Value,
                match.Groups[2].Value,
                int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static ExceptionFrame CreateFrame(string method, string? filePath, int? line)
    {
        var isUserCode = !IsGodotInternal(method);
        return new ExceptionFrame(method.Trim(), filePath, line, isUserCode);
    }

    private static bool IsGodotInternal(string method)
    {
        foreach (var prefix in InternalPrefixes)
        {
            if (method.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
