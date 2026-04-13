using System;
using Microsoft.Extensions.Logging;

namespace Wick.Runtime.Logging;

/// <summary>
/// A per-category <see cref="ILogger"/> that serializes every above-threshold log call
/// into a Wick envelope line on stderr.
/// </summary>
internal sealed class WickLogger : ILogger
{
    private readonly string _category;
    private readonly LogLevel _minimumLevel;

    public WickLogger(string category, LogLevel minimumLevel)
    {
        _category = category;
        _minimumLevel = minimumLevel;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter is null)
        {
            return;
        }

        try
        {
            var message = formatter(state, exception);
            var payload = new LogPayload(
                Category: _category,
                Level: logLevel.ToString(),
                Message: message,
                Exception: exception is null ? null : StackFrameParser.ToPayload(exception));
            WickEnvelope.WriteEnvelope("log", payload);
        }
        catch
        {
            // Logging paths must never throw.
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
