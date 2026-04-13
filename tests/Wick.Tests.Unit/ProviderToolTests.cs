using Wick.Providers.GDScript;
using Wick.Providers.CSharp;
using Wick.Providers.Godot;

namespace Wick.Tests.Unit;

public class ProviderToolTests
{
    [Fact]
    public void GDScriptStatus_ReturnsActiveStatus()
    {
        var result = GDScriptTools.GDScriptStatus();

        result.Should().Contain("\"provider\": \"GDScript\"");
        result.Should().Contain("\"status\": \"active\"");
    }

    [Fact]
    public void CSharpStatus_ReturnsActiveStatus()
    {
        var result = CSharpAnalysisTools.CSharpStatus();

        result.Should().Contain("\"provider\": \"C#/.NET\"");
        result.Should().Contain("\"status\": \"active\"");
    }

    [Fact]
    public void GodotStatus_ReturnsActiveStatus()
    {
        var result = GodotTools.GodotStatus();

        result.Should().Contain("\"provider\": \"Godot Engine\"");
        result.Should().Contain("\"status\": \"active\"");
    }

    [Fact]
    public void DetectLanguage_ReturnsCSharpForCsFile()
    {
        var result = GDScriptTools.DetectLanguage("Player.cs");

        result.Should().Contain("CSharp");
    }

    [Fact]
    public void DetectLanguage_ReturnsGDScriptForGdFile()
    {
        var result = GDScriptTools.DetectLanguage("player.gd");

        result.Should().Contain("GDScript");
    }
}
