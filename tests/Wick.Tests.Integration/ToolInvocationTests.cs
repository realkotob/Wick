using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Wick.Tests.Integration;

/// <summary>
/// Tests that individual MCP tools can be invoked and return structured results.
/// Uses tools that don't require a live Godot connection (status tools, catalog).
/// </summary>
public sealed class ToolInvocationTests
{
    [Fact]
    public async Task ToolCatalog_ReturnsNonEmptyResultAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act
        var result = await fixture.Client.CallToolAsync(
            "tool_catalog",
            new Dictionary<string, object?> { ["query"] = "" },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty("tool_catalog should return content describing available tool groups");

        var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        textContent.Should().NotBeNull("tool_catalog should return text content");
        textContent!.Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EditorStatus_ReturnsValidJsonAsync()
    {
        // Arrange — run with runtime group enabled to include editor_status
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core,runtime" });
        await fixture.InitializeAsync();

        // Act — editor_status works even without a Godot connection
        var result = await fixture.Client.CallToolAsync(
            "editor_status",
            new Dictionary<string, object?>(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();

        var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        textContent.Should().NotBeNull("editor_status should return text content");

        // The status should be parseable as JSON
        var parseAction = () => JsonDocument.Parse(textContent!.Text);
        parseAction.Should().NotThrow("editor_status should return valid JSON");
    }

    [Fact]
    public async Task GodotStatus_ReturnsValidJsonAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act — godot_status should also work without a live Godot connection
        var result = await fixture.Client.CallToolAsync(
            "godot_status",
            new Dictionary<string, object?>(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();

        var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        textContent.Should().NotBeNull("godot_status should return text content");
        textContent!.Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GdScriptStatus_ReturnsValidJsonAsync()
    {
        // Arrange
        await using var fixture = WickServerFixture.WithEnv(
            new Dictionary<string, string?> { ["WICK_GROUPS"] = "core" });
        await fixture.InitializeAsync();

        // Act
        var result = await fixture.Client.CallToolAsync(
            "gd_script_status",
            new Dictionary<string, object?>(),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().NotBeEmpty();

        var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        textContent.Should().NotBeNull("gd_script_status should return text content");
        textContent!.Text.Should().NotBeNullOrWhiteSpace();
    }
}
