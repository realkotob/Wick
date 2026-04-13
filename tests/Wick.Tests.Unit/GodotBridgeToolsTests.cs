using Wick.Providers.Godot;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Wick.Tests.Unit;

public class GodotBridgeToolsTests : IDisposable
{
    // The GodotBridgeManager constructor spins up clients aiming at local ports.
    // By default, unless Godot is randomly open in the background, they will be disconnected.
    private readonly GodotBridgeManager _manager = new GodotBridgeManager();

    public void Dispose()
    {
        _manager.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetSceneTree_WhenEditorOffline_ThrowsInvalidOperationException()
    {
        var tools = new GodotBridgeTools(_manager);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await tools.EditorSceneTree("editor"));

        Assert.Contains("not connected on port 6505", ex.Message);
    }

    [Fact]
    public async Task GetSceneTree_WhenRuntimeOffline_ThrowsInvalidOperationException()
    {
        var tools = new GodotBridgeTools(_manager);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await tools.EditorSceneTree("runtime"));

        Assert.Contains("not connected on port 7777", ex.Message);
    }

    [Fact]
    public async Task GetSceneTree_WhenTargetInvalid_ThrowsArgumentException()
    {
        var tools = new GodotBridgeTools(_manager);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await tools.EditorSceneTree("invalid_target_string"));

        Assert.Contains("Target must be either 'editor' or 'runtime'", ex.Message);
    }
}
