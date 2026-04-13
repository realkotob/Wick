using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Concrete <see cref="ISceneDispatchClient"/> that spawns
/// <c>godot --headless --script addons/wick/scene_ops.gd -- &lt;op&gt; &lt;json&gt;</c>
/// and reads the JSON result from stdout.
/// </summary>
public sealed partial class SceneDispatchClient : ISceneDispatchClient
{
    private readonly string _godotBinaryPath;
    private readonly string _projectPath;
    private readonly ILogger<SceneDispatchClient> _logger;
    private readonly TimeSpan _timeout;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public SceneDispatchClient(
        string godotBinaryPath,
        string projectPath,
        ILogger<SceneDispatchClient> logger,
        TimeSpan? timeout = null)
    {
        _godotBinaryPath = godotBinaryPath;
        _projectPath = projectPath;
        _logger = logger;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Debug,
        Message = "Dispatching scene operation '{Operation}' via headless Godot")]
    private partial void LogDispatch(string operation);

    [LoggerMessage(EventId = 201, Level = LogLevel.Warning,
        Message = "Scene dispatch process exited with code {ExitCode}")]
    private partial void LogBadExit(int exitCode);

    [LoggerMessage(EventId = 202, Level = LogLevel.Error,
        Message = "Scene dispatch failed for operation '{Operation}'")]
    private partial void LogDispatchError(string operation, Exception ex);

    public async Task<SceneModifyResult> DispatchAsync(
        string operation,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken = default)
    {
        var argsJson = JsonSerializer.Serialize(args, JsonOptions);
        LogDispatch(operation);

        var startInfo = new ProcessStartInfo
        {
            FileName = _godotBinaryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(_projectPath);
        startInfo.ArgumentList.Add("--script");
        startInfo.ArgumentList.Add("addons/wick/scene_ops.gd");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add(argsJson);

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                LogBadExit(process.ExitCode);
            }

            return ParseResult(stdout, operation);
        }
        catch (OperationCanceledException)
        {
            return new SceneModifyResult(
                Ok: false, ScenePath: null, NodeName: null, NodeType: null,
                Error: $"Operation '{operation}' timed out after {_timeout.TotalSeconds}s",
                ErrorCode: "timeout");
        }
        catch (Exception ex)
        {
            LogDispatchError(operation, ex);
            return new SceneModifyResult(
                Ok: false, ScenePath: null, NodeName: null, NodeType: null,
                Error: $"Scene dispatch failed for operation '{operation}'. Check server logs for details.",
                ErrorCode: "internal");
        }
    }

    private static SceneModifyResult ParseResult(string stdout, string operation)
    {
        // The GDScript writes a single JSON line. Find the last non-empty line
        // (Godot may print warnings/debug output before it).
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{'))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                var ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
                if (!ok)
                {
                    string? errorCode = null;
                    string? errorMsg = null;
                    if (root.TryGetProperty("error", out var errObj) && errObj.ValueKind == JsonValueKind.Object)
                    {
                        errorCode = errObj.TryGetProperty("code", out var c) ? c.GetString() : null;
                        errorMsg = errObj.TryGetProperty("message", out var m) ? m.GetString() : null;
                    }
                    return new SceneModifyResult(Ok: false, ScenePath: null, NodeName: null, NodeType: null,
                        Error: errorMsg ?? "Unknown error", ErrorCode: errorCode ?? "internal");
                }

                string? scenePath = null;
                string? nodeName = null;
                string? nodeType = null;
                if (root.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
                {
                    scenePath = result.TryGetProperty("scene_path", out var sp) ? sp.GetString() : null;
                    nodeName = result.TryGetProperty("node_name", out var nn) ? nn.GetString() : null;
                    nodeType = result.TryGetProperty("node_type", out var nt) ? nt.GetString() : null;
                }

                return new SceneModifyResult(Ok: true, ScenePath: scenePath, NodeName: nodeName,
                    NodeType: nodeType, Error: null, ErrorCode: null);
            }
            catch (JsonException)
            {
                continue;
            }
        }

        return new SceneModifyResult(Ok: false, ScenePath: null, NodeName: null, NodeType: null,
            Error: $"No valid JSON output from '{operation}' dispatch",
            ErrorCode: "internal");
    }
}
