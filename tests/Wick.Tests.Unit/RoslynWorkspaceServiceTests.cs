using Microsoft.Extensions.Logging.Abstractions;
using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

public sealed class RoslynWorkspaceServiceTests : IAsyncLifetime
{
    private readonly RoslynWorkspaceService _service = new(NullLogger<RoslynWorkspaceService>.Instance);
    private string _fixturePath = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(RoslynWorkspaceServiceTests).Assembly.Location)!;
        _fixturePath = Path.Combine(assemblyDir, "Fixtures", "SampleProject", "SampleProject.csproj");
        await _service.InitializeAsync(_fixturePath, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _service.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void Initialize_LoadsProject_IsLoadedTrue()
    {
        _service.IsLoaded.Should().BeTrue();
        _service.LoadError.Should().BeNull();
    }

    [Fact]
    public void GetSourceContext_KnownMethodLine_ReturnsMethodBody()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(RoslynWorkspaceServiceTests).Assembly.Location)!;
        var playerControllerPath = Path.Combine(assemblyDir, "Fixtures", "SampleProject", "PlayerController.cs");

        // Line 15 is "_health -= amount;" inside TakeDamage
        var context = _service.GetSourceContext(playerControllerPath, 15);

        context.Should().NotBeNull();
        context!.MethodBody.Should().Contain("TakeDamage");
        context.EnclosingType.Should().Contain("PlayerController");
    }

    [Fact]
    public async Task GetCallers_KnownMethod_ReturnsCallSites()
    {
        var callers = await _service.GetCallersAsync("PlayerController", "TakeDamage");

        callers.Should().NotBeEmpty();
        callers.Should().Contain(c => c.Contains("EnemyAI", StringComparison.Ordinal) && c.Contains("Attack", StringComparison.Ordinal));
    }

    [Fact]
    public void GetSourceContext_UnknownFile_ReturnsNull()
    {
        var context = _service.GetSourceContext("/nonexistent/path/Fake.cs", 10);

        context.Should().BeNull();
    }
}
