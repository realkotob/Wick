using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Concrete <see cref="IGameLauncher"/> that spawns a headless Godot subprocess
/// and starts a <see cref="ProcessExceptionSource"/> reading its stderr into the
/// Sub-spec A exception pipeline.
/// </summary>
public sealed partial class ProcessGameLauncher : IGameLauncher
{
    private readonly string _godotBinaryPath;
    private readonly string _projectPath;
    private readonly ExceptionBuffer _exceptionBuffer;
    private readonly LogBuffer _logBuffer;
    private readonly ExceptionEnricher _enricher;
    private readonly ILogger<ProcessGameLauncher> _logger;
    private readonly InProcessBridgeClientFactory? _bridgeFactory;

    public ProcessGameLauncher(
        string godotBinaryPath,
        string projectPath,
        ExceptionBuffer exceptionBuffer,
        LogBuffer logBuffer,
        ExceptionEnricher enricher,
        ILogger<ProcessGameLauncher> logger,
        InProcessBridgeClientFactory? bridgeFactory = null)
    {
        _godotBinaryPath = godotBinaryPath;
        _projectPath = projectPath;
        _exceptionBuffer = exceptionBuffer;
        _logBuffer = logBuffer;
        _enricher = enricher;
        _logger = logger;
        _bridgeFactory = bridgeFactory;
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning,
        Message = "Game process capture loop terminated with an error")]
    private partial void LogCaptureLoopError(Exception ex);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning,
        Message = "Error while stopping game process")]
    private partial void LogStopError(Exception ex);

    public LaunchedGame Launch(string? scene, bool headless, IReadOnlyList<string> extraArgs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _godotBinaryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (headless) startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(_projectPath);
        if (!string.IsNullOrEmpty(scene))
        {
            startInfo.ArgumentList.Add(scene);
        }
        foreach (var arg in extraArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        var startedAt = DateTimeOffset.UtcNow;

        var cts = new CancellationTokenSource();
        var source = new ProcessExceptionSource(
            _godotBinaryPath, _projectPath, scene, _logBuffer,
            onHandshake: _bridgeFactory is null ? null : port => _bridgeFactory.InstallFromHandshake(port));

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var raw in source.CaptureAsync(cts.Token))
                {
                    var enriched = await _enricher.EnrichAsync(raw).ConfigureAwait(false);
                    _exceptionBuffer.Add(enriched);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on stop.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCaptureLoopError(ex);
            }
        }, cts.Token);

        var pid = process.Id;
        Action onStop = () =>
        {
            try
            {
                cts.Cancel();
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.WaitForExit(2000);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                LogStopError(ex);
            }
            finally
            {
                process.Dispose();
                cts.Dispose();
            }
        };

        return new LaunchedGame(pid, startedAt, onStop);
    }
}
