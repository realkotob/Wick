using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Wick.Runtime;
using Wick.Runtime.Bridge;
using Wick.Runtime.Hooks;
using Wick.Runtime.Logging;

namespace Wick.Tests.Unit.Runtime;

[Collection("ConsoleError")]
public sealed class WickRuntimeHooksTests
{
    [Fact]
    public void TaskSchedulerExceptionHook_InstallIsIdempotent()
    {
        var hook = new TaskSchedulerExceptionHook();
        hook.Install();
        hook.Install();
        hook.Uninstall();
        hook.Uninstall();
        // No exceptions = success.
    }

    [Fact]
    public void AppDomainExceptionHook_InstallIsIdempotent()
    {
        var hook = new AppDomainExceptionHook();
        hook.Install();
        hook.Install();
        hook.Uninstall();
        hook.Uninstall();
    }

    [Fact]
    public void WickLoggerProvider_SuppressesBelowMinimumLevel()
    {
        var captured = new System.IO.StringWriter();
        var original = System.Console.Error;
        try
        {
            System.Console.SetError(captured);
            var provider = new WickLoggerProvider(LogLevel.Warning);
            var logger = provider.CreateLogger("T");

#pragma warning disable CA1848
            logger.LogDebug("should be suppressed");
            logger.LogWarning("should be emitted");
#pragma warning restore CA1848
        }
        finally
        {
            System.Console.SetError(original);
        }

        var output = captured.ToString();
        output.Should().NotContain("should be suppressed");
        // Find our own line (other parallel tests may write to the shared error stream).
        output.Split('\n').Any(l => l.Contains("should be emitted")).Should().BeTrue();
    }

    [Fact]
    public void WickLogger_EmitsEnvelopeWithCategoryAndLevel()
    {
        var captured = new System.IO.StringWriter();
        var original = System.Console.Error;
        try
        {
            System.Console.SetError(captured);
            var provider = new WickLoggerProvider();
            var logger = provider.CreateLogger("MyGame.Combat");
#pragma warning disable CA1848
            logger.LogError(new System.InvalidOperationException("boom"), "failed to shoot");
#pragma warning restore CA1848
        }
        finally
        {
            System.Console.SetError(original);
        }

        // Other parallel tests may share Console.Error; extract our envelope line.
        var output = captured.ToString();
        var envelopeLine = output.Split('\n').FirstOrDefault(l => l.Contains("MyGame.Combat"));
        envelopeLine.Should().NotBeNull();
        envelopeLine!.TrimStart().Should().StartWith("{\"__wick\":1");

        using var doc = JsonDocument.Parse(envelopeLine.Trim());
        doc.RootElement.GetProperty("kind").GetString().Should().Be("log");
        var payload = doc.RootElement.GetProperty("payload");
        payload.GetProperty("category").GetString().Should().Be("MyGame.Combat");
        payload.GetProperty("level").GetString().Should().Be("Error");
        payload.GetProperty("message").GetString().Should().Contain("failed to shoot");
        payload.GetProperty("exception").GetProperty("type").GetString().Should().Contain("InvalidOperationException");
    }

    [Fact]
    public void WickRuntimeOptions_FromEnvironment_ReadsPort()
    {
        System.Environment.SetEnvironmentVariable("WICK_RUNTIME_PORT", "9123");
        try
        {
            var opts = WickRuntimeOptions.FromEnvironment();
            opts.BridgePort.Should().Be(9123);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("WICK_RUNTIME_PORT", null);
        }
    }

    [Fact]
    public async Task MainThreadDispatcher_Run_ReturnsResultFromMainThread()
    {
        var dispatcher = MainThreadDispatcher.Instance;
        dispatcher.Tick(); // drain any leftover state from other tests
        // Background thread calls Run, this thread Ticks to simulate main thread.
        var task = System.Threading.Tasks.Task.Run(() =>
            dispatcher.Run(() => 21 * 2, System.TimeSpan.FromSeconds(5)));

        var deadline = System.DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && System.DateTime.UtcNow < deadline)
        {
            dispatcher.Tick();
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        task.IsCompletedSuccessfully.Should().BeTrue();
        var result = await task;
        result.Should().Be(42);
    }

    [Fact]
    public void MainThreadDispatcher_Schedule_QueuesWork()
    {
        var dispatcher = MainThreadDispatcher.Instance;
        dispatcher.Tick(); // drain anything leftover from other tests
        int counter = 0;
        dispatcher.Schedule(() => counter++);
        dispatcher.Schedule(() => counter++);
        dispatcher.PendingCount.Should().BeGreaterThanOrEqualTo(2);
        dispatcher.Tick();
        counter.Should().Be(2);
    }
}
