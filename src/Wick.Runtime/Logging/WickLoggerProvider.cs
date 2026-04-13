using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Wick.Runtime.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that routes every structured log through the Wick stderr
/// envelope transport. Install it on the user's ILoggerFactory in the Godot project and
/// every <c>logger.LogError(ex, "...")</c> call becomes a Wick event.
/// </summary>
public sealed class WickLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, WickLogger> _loggers = new(StringComparer.Ordinal);
    private readonly LogLevel _minimumLevel;

    public WickLoggerProvider(LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new WickLogger(name, _minimumLevel));

    public void Dispose()
    {
        _loggers.Clear();
    }
}
