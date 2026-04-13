using Wick.Providers.Godot;

namespace Wick.Tests.Unit.Providers.Godot;

public sealed class GameProcessManagerTests
{
    [Fact]
    public void Status_WhenIdle_ReturnsNotRunning()
    {
        var manager = new GameProcessManager();
        var status = manager.Status;
        status.IsRunning.Should().BeFalse();
        status.Pid.Should().BeNull();
    }

    [Fact]
    public void TryLaunch_WhenIdle_MarksRunningAndReturnsTrue()
    {
        var manager = new GameProcessManager();
        var result = manager.TryLaunch(pid: 4242, DateTimeOffset.UtcNow, onStop: () => { });
        result.Should().BeTrue();
        manager.Status.IsRunning.Should().BeTrue();
        manager.Status.Pid.Should().Be(4242);
    }

    [Fact]
    public void TryLaunch_WhenAlreadyRunning_ReturnsFalse()
    {
        var manager = new GameProcessManager();
        manager.TryLaunch(pid: 1, DateTimeOffset.UtcNow, onStop: () => { });
        var second = manager.TryLaunch(pid: 2, DateTimeOffset.UtcNow, onStop: () => { });
        second.Should().BeFalse();
        manager.Status.Pid.Should().Be(1);
    }

    [Fact]
    public void Stop_WhenRunning_InvokesOnStopAndClearsState()
    {
        var manager = new GameProcessManager();
        var stopCalled = false;
        manager.TryLaunch(pid: 9, DateTimeOffset.UtcNow, onStop: () => stopCalled = true);

        var result = manager.Stop(exitCode: 0);

        stopCalled.Should().BeTrue();
        result.Stopped.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        manager.Status.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Stop_WhenIdle_ReturnsStoppedFalse()
    {
        var manager = new GameProcessManager();
        var result = manager.Stop(exitCode: null);
        result.Stopped.Should().BeFalse();
    }
}
