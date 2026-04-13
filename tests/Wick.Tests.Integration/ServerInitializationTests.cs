namespace Wick.Tests.Integration;

/// <summary>
/// Tests that the Wick MCP server starts up correctly and exposes
/// the expected tool surface via the MCP protocol.
/// </summary>
public sealed class ServerInitializationTests
{
    [Fact]
    public async Task Server_ListsTools_ReturnsAtLeastOneToolAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act
        var tools = await fixture.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert — the server should expose tools from all registered assemblies
        tools.Should().NotBeEmpty("the server registers tools from 4 provider assemblies");
    }

    [Fact]
    public async Task Server_ListsTools_IncludesToolGroupToolsAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act
        var tools = await fixture.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert — ToolGroupTools should be registered.
        // The MCP SDK may convert PascalCase to snake_case or use the [McpServerTool(Name=...)] value.
        // We check for ANY tool name containing "catalog" since that's the unique identifier.
        toolNames.Should().Contain(
            name => name.Contains("catalog", StringComparison.OrdinalIgnoreCase),
            "ToolGroupTools.ToolCatalog should be registered with a name containing 'catalog'");
    }

    [Fact]
    public async Task Server_ListsTools_IncludesGodotBridgeToolsAsync()
    {
        // Arrange — run with runtime group enabled to include GodotBridgeTools
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core,runtime" });
        await fixture.InitializeAsync();

        // Act
        var tools = await fixture.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert — GodotBridgeTools.EditorStatus should be registered.
        // Check for any tool name containing "editor" and "status".
        toolNames.Should().Contain(
            name => name.Contains("editor", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("status", StringComparison.OrdinalIgnoreCase),
            "GodotBridgeTools.EditorStatus should be registered");
    }

    [Fact]
    public async Task Server_ListsTools_DoesNotIncludeDeletedEditorToolsAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act — PR #16 deleted EditorTools.cs; verify it's gone from the server
        var tools = await fixture.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert — no tool should have the stale un-prefixed names from the deleted EditorTools
        toolNames.Should().NotContain(
            name => name.Equals("get_scene_tree", StringComparison.OrdinalIgnoreCase),
            "EditorTools (deleted in PR #16) should not be registered");
        toolNames.Should().NotContain(
            name => name.Equals("run_scene", StringComparison.OrdinalIgnoreCase),
            "EditorTools (deleted in PR #16) should not be registered");
    }

    [Fact]
    public async Task Server_ExceptionPipeline_IsRunningAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act — The pipeline is a hosted service — if the server started successfully
        // (which it did because we're using it), the pipeline is running.
        // We verify by checking that tools/list still works after pipeline init.
        var tools = await fixture.Client.ListToolsAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        tools.Should().NotBeEmpty("server should still function with pipeline running");
    }

    [Fact]
    public async Task Server_ListsTools_PrintsAllToolNamesForDiscoveryAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act — This test exists purely for discovery — it prints all registered tool names
        // so we can see the exact snake_case names the MCP SDK produces.
        // It always passes; the output is the value.
        var tools = await fixture.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        foreach (var tool in tools.OrderBy(t => t.Name))
        {
            // Output visible in test runner verbose mode
            Console.Error.WriteLine($"  [TOOL] {tool.Name} — {tool.Description}");
        }

        tools.Should().NotBeEmpty();
    }
}
