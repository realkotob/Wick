using Wick.Core;
using Wick.Providers.Godot;

namespace Wick.Tests.Unit;

public sealed class ProcessExceptionSourceTests
{
    [Fact]
    public async Task CaptureAsync_NonExistentBinary_YieldsNothing()
    {
        var source = new ProcessExceptionSource(
            "/nonexistent/godot-binary",
            "/nonexistent/project");

        var exceptions = new List<RawException>();
        await foreach (var ex in source.CaptureAsync(TestContext.Current.CancellationToken))
        {
            exceptions.Add(ex);
        }

        exceptions.Should().BeEmpty("a non-existent binary should fail gracefully");
    }

    [Fact]
    public async Task CaptureAsync_ProcessWithNoErrors_YieldsNothing()
    {
        // Use 'echo' as a process that exits immediately with no stderr output
        var source = new ProcessExceptionSource(
            "echo",
            "hello");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var exceptions = new List<RawException>();
        await foreach (var ex in source.CaptureAsync(cts.Token))
        {
            exceptions.Add(ex);
        }

        exceptions.Should().BeEmpty("a process with no ERROR: output should yield no exceptions");
    }
}
