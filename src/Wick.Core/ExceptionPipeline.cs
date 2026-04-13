using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wick.Core;

/// <summary>
/// Background service that drains all registered <see cref="IExceptionSource"/> instances
/// in parallel, enriches each <see cref="RawException"/> via <see cref="ExceptionEnricher"/>,
/// and deposits the result into <see cref="ExceptionBuffer"/>.
/// </summary>
public sealed partial class ExceptionPipeline : BackgroundService
{
    private readonly IEnumerable<IExceptionSource> _sources;
    private readonly ExceptionEnricher _enricher;
    private readonly ExceptionBuffer _buffer;
    private readonly ILogger<ExceptionPipeline> _logger;

    public ExceptionPipeline(
        IEnumerable<IExceptionSource> sources,
        ExceptionEnricher enricher,
        ExceptionBuffer buffer,
        ILogger<ExceptionPipeline> logger)
    {
        _sources = sources;
        _enricher = enricher;
        _buffer = buffer;
        _logger = logger;
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning,
        Message = "Exception enrichment failed, storing raw exception only")]
    private partial void LogEnrichmentFailed(Exception ex);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error,
        Message = "Exception source {SourceType} terminated unexpectedly")]
    private partial void LogSourceFailed(Exception ex, string sourceType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start consuming from all sources in parallel
        var tasks = _sources.Select(source => ConsumeSourceAsync(source, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ConsumeSourceAsync(IExceptionSource source, CancellationToken ct)
    {
        try
        {
            await foreach (var raw in source.CaptureAsync(ct))
            {
                try
                {
                    var enriched = await _enricher.EnrichAsync(raw).ConfigureAwait(false);
                    _buffer.Add(enriched);
                }
                catch (Exception ex)
                {
                    LogEnrichmentFailed(ex);
                    // Still add the raw exception without enrichment
                    _buffer.Add(new EnrichedException { Raw = raw });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            LogSourceFailed(ex, source.GetType().Name);
        }
    }
}
