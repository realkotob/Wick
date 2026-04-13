using System;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wick.Runtime.Bridge;
using Wick.Runtime.Hooks;
using Wick.Runtime.Logging;

namespace Wick.Runtime;

/// <summary>
/// One-line entry point. From a Godot autoload C# script:
/// <code>
/// public override void _Ready()
/// {
///     Wick.Runtime.WickRuntime.Install();
/// }
///
/// public override void _Process(double delta)
/// {
///     Wick.Runtime.WickRuntime.Tick();
/// }
/// </code>
/// </summary>
public static class WickRuntime
{
    private static int s_installed;
    private static TaskSchedulerExceptionHook? s_taskHook;
    private static AppDomainExceptionHook? s_appDomainHook;
    private static WickBridgeServer? s_bridgeServer;
    private static WickLoggerProvider? s_loggerProvider;
    private static EventHandler? s_processExitHandler;

    /// <summary>The logger provider installed by <see cref="Install"/>, or null if none.</summary>
    public static WickLoggerProvider? LoggerProvider => s_loggerProvider;

    /// <summary>
    /// Installs all configured hooks and starts the bridge server. Idempotent — a second
    /// call is a no-op and returns the same options that were applied.
    /// </summary>
    public static void Install(WickRuntimeOptions? options = null)
    {
        if (Interlocked.Exchange(ref s_installed, 1) == 1)
        {
            return;
        }

        var opts = (options ?? WickRuntimeOptions.FromEnvironment()).ResolvedWithDefaults();

        if (opts.EnableTaskSchedulerHook)
        {
            s_taskHook = new TaskSchedulerExceptionHook();
            s_taskHook.Install();
        }
        if (opts.EnableAppDomainHook)
        {
            s_appDomainHook = new AppDomainExceptionHook();
            s_appDomainHook.Install();
        }

        s_loggerProvider = new WickLoggerProvider(opts.MinimumLogLevel);

        if (opts.EnableLiveBridge)
        {
            var bridge = opts.SceneBridge ?? (ISceneBridge)Activator.CreateInstance(
                typeof(ReflectionSceneBridge), nonPublic: true)!;
            var dispatcher = MainThreadDispatcher.Instance;
            var handlers = new WickBridgeHandlers(bridge, dispatcher);
            s_bridgeServer = new WickBridgeServer(handlers);
            try
            {
                s_bridgeServer.Start(opts.BridgePort);
            }
            catch (Exception ex)
            {
                // A bound port collision should not kill the user's game. Emit a handshake
                // with port=0 so the server-side parser can log the failure.
                WickEnvelope.WriteEnvelope("handshake_failed", new { error = ex.Message });
                s_bridgeServer = null;
            }
        }

        var version = opts.Version ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        var boundPort = s_bridgeServer?.Port ?? opts.BridgePort;
        WickEnvelope.WriteEnvelope("handshake", new HandshakePayload(boundPort, version));

        s_processExitHandler = (_, _) => Uninstall();
        AppDomain.CurrentDomain.ProcessExit += s_processExitHandler;
    }

    /// <summary>
    /// Drains the main-thread dispatcher queue. Users call this from their autoload's
    /// <c>_Process(delta)</c> so live-bridge RPC work runs on the main thread.
    /// </summary>
    public static void Tick() => MainThreadDispatcher.Instance.Tick();

    /// <summary>
    /// Tears down hooks and stops the bridge server. Normally triggered by
    /// <see cref="AppDomain.ProcessExit"/>; exposed for tests and users who need to
    /// hot-reload the library.
    /// </summary>
    public static void Uninstall()
    {
        if (Interlocked.Exchange(ref s_installed, 0) == 0)
        {
            return;
        }
        try { s_taskHook?.Uninstall(); } catch { }
        try { s_appDomainHook?.Uninstall(); } catch { }
        try { s_bridgeServer?.Stop(); } catch { }
        try { s_loggerProvider?.Dispose(); } catch { }
        if (s_processExitHandler is not null)
        {
            try { AppDomain.CurrentDomain.ProcessExit -= s_processExitHandler; } catch { }
            s_processExitHandler = null;
        }
        s_taskHook = null;
        s_appDomainHook = null;
        s_bridgeServer = null;
        s_loggerProvider = null;
    }
}

/// <summary>Configuration surface for <see cref="WickRuntime.Install"/>.</summary>
public sealed record WickRuntimeOptions
{
    /// <summary>Port to bind the live-query TCP bridge on. 0 = ephemeral (test use).</summary>
    public int BridgePort { get; init; } = 7878;

    /// <summary>Enable the in-process TCP bridge server. Default true.</summary>
    public bool EnableLiveBridge { get; init; } = true;

    /// <summary>Register the TaskScheduler.UnobservedTaskException hook. Default true.</summary>
    public bool EnableTaskSchedulerHook { get; init; } = true;

    /// <summary>Register the AppDomain.UnhandledException hook. Default true.</summary>
    public bool EnableAppDomainHook { get; init; } = true;

    /// <summary>Minimum log level forwarded to the Wick envelope stream. Trace/Debug muted by default.</summary>
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;

    /// <summary>Optional version string included in the handshake. Falls back to assembly version.</summary>
    public string? Version { get; init; }

    /// <summary>Test seam: inject a stub scene bridge instead of the reflection implementation.</summary>
    public ISceneBridge? SceneBridge { get; init; }

    /// <summary>
    /// Populates defaults from the process environment (<c>WICK_RUNTIME_PORT</c>) but leaves
    /// all feature toggles at their code defaults.
    /// </summary>
    public static WickRuntimeOptions FromEnvironment()
    {
        var portEnv = Environment.GetEnvironmentVariable("WICK_RUNTIME_PORT");
        if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var parsed))
        {
            return new WickRuntimeOptions { BridgePort = parsed };
        }
        return new WickRuntimeOptions();
    }

    internal WickRuntimeOptions ResolvedWithDefaults() => this;
}
