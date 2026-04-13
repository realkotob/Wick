using System.Threading.Channels;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Exception source fed by the TCP bridge from the GDScript addon.
/// When the addon forwards a Godot error report, <see cref="OnExceptionReported"/>
/// pushes the raw text into an unbounded channel; <see cref="CaptureAsync"/>
/// drains it through the parser into <see cref="RawException"/> instances.
/// </summary>
public sealed class BridgeExceptionSource : IExceptionSource
{
    private readonly Channel<string> _errorChannel = Channel.CreateUnbounded<string>();

    /// <summary>
    /// Called by the bridge when the GDScript addon forwards an error report.
    /// </summary>
    public void OnExceptionReported(string rawErrorText)
    {
        _errorChannel.Writer.TryWrite(rawErrorText);
    }

    public async IAsyncEnumerable<RawException> CaptureAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var rawText in _errorChannel.Reader.ReadAllAsync(ct))
        {
            var parsed = GodotExceptionParser.Parse(rawText);
            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }
}
