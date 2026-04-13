using Microsoft.Extensions.Logging.Abstractions;
using Wick.Server.Tools;

namespace Wick.Tests.Unit.Tools;

public sealed class ToolGroupResolverTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    private static readonly string[] CoreOnly = ["core"];
    private static readonly string[] CoreAndRuntime = ["core", "runtime"];
    private static readonly string[] AllKnown = ["core", "runtime", "scene", "csharp", "build"];
    private static readonly string[] CSharpOnly = ["csharp"];

    [Fact]
    public void Resolve_NullCliAndEnv_ReturnsCoreOnly()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: null, envValue: null, Logger);
        result.Should().BeEquivalentTo(CoreOnly);
    }

    [Fact]
    public void Resolve_EmptyCliAndEnv_ReturnsCoreOnly()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: "", envValue: "   ", Logger);
        result.Should().BeEquivalentTo(CoreOnly);
    }

    [Fact]
    public void Resolve_EnvWithCoreAndRuntime_ReturnsBoth()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: null, envValue: "core,runtime", Logger);
        result.Should().BeEquivalentTo(CoreAndRuntime);
    }

    [Fact]
    public void Resolve_EnvAll_ReturnsAllKnownGroups()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: null, envValue: "all", Logger);
        result.Should().BeEquivalentTo(AllKnown);
    }

    [Fact]
    public void Resolve_UnknownGroupInEnv_IsSkippedAndRestKept()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: null, envValue: "core,bogus,runtime", Logger);
        result.Should().BeEquivalentTo(CoreAndRuntime);
    }

    [Fact]
    public void Resolve_CliOverridesEnvEntirely()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: "csharp", envValue: "core,runtime", Logger);
        result.Should().BeEquivalentTo(CSharpOnly);
    }

    [Fact]
    public void Resolve_MixedCaseInput_IsNormalized()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: null, envValue: "Core,RUNTIME", Logger);
        result.Should().BeEquivalentTo(CoreAndRuntime);
    }

    [Fact]
    public void Resolve_AllUnknown_FallsBackToCore()
    {
        var result = ToolGroupResolver.Resolve(cliFlag: "bogus,also_bogus", envValue: null, Logger);
        result.Should().BeEquivalentTo(CoreOnly);
    }
}
