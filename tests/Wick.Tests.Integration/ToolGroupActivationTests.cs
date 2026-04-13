namespace Wick.Tests.Integration;

/// <summary>
/// Integration tests that verify WICK_GROUPS env var correctly gates
/// which tools are advertised by the server at runtime.
///
/// Each test spins up its own server process (not shared via IClassFixture)
/// because each needs a different value for WICK_GROUPS.
/// The harness <see cref="WickServerFixture"/> accepts env overrides via its constructor;
/// null values remove the variable from the subprocess environment.
/// </summary>
public sealed class ToolGroupActivationTests
{
    /// <summary>
    /// When WICK_GROUPS includes "runtime", the server should expose RuntimeTools
    /// (runtime_status, runtime_get_log_tail, etc.) and ToolGroupTools (tool_groups, tool_catalog).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Server_WithRuntimeGroup_ExposesRuntimeStatusToolAsync()
    {
        // Arrange — start a fresh server with WICK_GROUPS=core,runtime
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?>
            {
                ["WICK_GROUPS"] = "core,runtime",
            });
        await fixture.InitializeAsync();

        // Act
        var tools = await fixture.Client.ListToolsAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert — runtime group tools are present
        toolNames.Should().Contain(
            "runtime_status",
            "runtime group should expose RuntimeTools.RuntimeStatus as 'runtime_status'");

        // Assert — introspection tools are always present
        toolNames.Should().Contain(
            "tool_groups",
            "ToolGroupTools.ToolGroups should always be registered regardless of active groups");
    }

    /// <summary>
    /// When WICK_GROUPS is not set (or has been explicitly removed from the environment),
    /// the server falls back to the default group set which does NOT include "runtime".
    /// Therefore runtime_status must NOT appear, but tool_groups (always-on introspection) must appear.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task Server_DefaultGroups_DoesNotExposeRuntimeToolsAsync()
    {
        // Arrange — start a fresh server with WICK_GROUPS explicitly removed (null → unset).
        // Even if the parent shell has WICK_GROUPS set, the null value removes it from the
        // subprocess environment per StdioClientTransportOptions.EnvironmentVariables semantics.
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?>
            {
                ["WICK_GROUPS"] = null,
            });
        await fixture.InitializeAsync();

        // Act
        var tools = await fixture.Client.ListToolsAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert — runtime group tools are absent from the default group set
        toolNames.Should().NotContain(
            "runtime_status",
            "runtime_status is gated on the 'runtime' group, which is not active by default");

        // Assert — introspection tools are always present (never gated)
        toolNames.Should().Contain(
            "tool_groups",
            "ToolGroupTools.ToolGroups should always be registered regardless of active groups");
    }
}
