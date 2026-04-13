using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Wick.Runtime;

/// <summary>
/// Parses a .NET <see cref="Exception.StackTrace"/> string into <see cref="FramePayload"/>s.
/// We prefer <see cref="StackTrace"/> reflection when we have a live Exception (accurate
/// source info in Debug builds). As a fallback we parse the text string, which works even
/// after marshalling and is good enough for IsUserCode heuristics.
/// </summary>
internal static class StackFrameParser
{
    /// <summary>User-code heuristic: everything that isn't in the BCL / Godot runtime.</summary>
    private static readonly string[] s_systemPrefixes =
    [
        "System.",
        "Microsoft.",
        "Godot.",
        "Mono.",
    ];

    public static IReadOnlyList<FramePayload> FromException(Exception ex)
    {
        if (ex is null)
        {
            return Array.Empty<FramePayload>();
        }

        try
        {
            var trace = new StackTrace(ex, fNeedFileInfo: true);
            var frames = trace.GetFrames();
            var result = new List<FramePayload>(frames.Length);
            foreach (var f in frames)
            {
                var method = f.GetMethod();
                if (method is null)
                {
                    continue;
                }
                var typeName = method.DeclaringType?.FullName ?? "<unknown>";
                var signature = $"{typeName}.{method.Name}";
                var file = f.GetFileName();
                var line = f.GetFileLineNumber();
                result.Add(new FramePayload(
                    Method: signature,
                    FilePath: string.IsNullOrEmpty(file) ? null : file,
                    Line: line > 0 ? line : null,
                    IsUserCode: IsUserCode(typeName)));
            }
            if (result.Count > 0)
            {
                return result;
            }
        }
        catch
        {
            // Fall through to text parse
        }

        return FromText(ex.StackTrace);
    }

    public static IReadOnlyList<FramePayload> FromText(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return Array.Empty<FramePayload>();
        }

        var frames = new List<FramePayload>();
        foreach (var rawLine in stackTrace.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || !line.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            // "at Namespace.Type.Method(args) in /path/to/file.cs:line 42"
            var body = line.Substring(3);
            string? filePath = null;
            int? lineNumber = null;
            string methodPart = body;

            var inIdx = body.LastIndexOf(" in ", StringComparison.Ordinal);
            if (inIdx >= 0)
            {
                methodPart = body.Substring(0, inIdx);
                var fileRef = body.Substring(inIdx + 4);
                var colonIdx = fileRef.LastIndexOf(':');
                if (colonIdx >= 0)
                {
                    filePath = fileRef.Substring(0, colonIdx);
                    var lineStr = fileRef.Substring(colonIdx + 1);
                    if (lineStr.StartsWith("line ", StringComparison.Ordinal))
                    {
                        lineStr = lineStr.Substring(5);
                    }
                    if (int.TryParse(lineStr, out var parsed))
                    {
                        lineNumber = parsed;
                    }
                }
                else
                {
                    filePath = fileRef;
                }
            }

            // Pull the declaring type off the method part to do the user-code check.
            var parenIdx = methodPart.IndexOf('(');
            var methodNoArgs = parenIdx >= 0 ? methodPart.Substring(0, parenIdx) : methodPart;
            var lastDot = methodNoArgs.LastIndexOf('.');
            var typeName = lastDot >= 0 ? methodNoArgs.Substring(0, lastDot) : methodNoArgs;

            frames.Add(new FramePayload(
                Method: methodPart,
                FilePath: filePath,
                Line: lineNumber,
                IsUserCode: IsUserCode(typeName)));
        }

        return frames;
    }

    private static bool IsUserCode(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }
        foreach (var prefix in s_systemPrefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public static ExceptionPayload ToPayload(Exception ex)
    {
        return new ExceptionPayload(
            Type: ex.GetType().FullName ?? ex.GetType().Name,
            Message: ex.Message,
            StackTrace: ex.StackTrace,
            Frames: FromException(ex));
    }
}
