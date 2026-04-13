using Wick.Core;

namespace Wick.Tests.Unit;

public class LanguageRouterTests
{
    [Theory]
    [InlineData("player.gd", LanguageContext.GDScript)]
    [InlineData("Player.cs", LanguageContext.CSharp)]
    [InlineData("main.tscn", LanguageContext.GodotResource)]
    [InlineData("theme.tres", LanguageContext.GodotResource)]
    [InlineData("project.godot", LanguageContext.GodotResource)]
    [InlineData("readme.md", LanguageContext.Unknown)]
    [InlineData("", LanguageContext.Unknown)]
    public void ResolveLanguage_ReturnsCorrectContext(string filePath, LanguageContext expected)
    {
        var result = LanguageRouter.ResolveLanguage(filePath);

        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveLanguage_IsCaseInsensitive()
    {
        LanguageRouter.ResolveLanguage("Player.GD").Should().Be(LanguageContext.GDScript);
        LanguageRouter.ResolveLanguage("Player.CS").Should().Be(LanguageContext.CSharp);
        LanguageRouter.ResolveLanguage("Main.TSCN").Should().Be(LanguageContext.GodotResource);
    }
}
