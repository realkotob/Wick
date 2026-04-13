using Microsoft.Extensions.Logging.Abstractions;
using Wick.Core;
using Wick.Providers.CSharp;

namespace Wick.Tests.Unit;

/// <summary>
/// Tests for the Sub-spec D C# analysis tools: <c>csharp_find_symbol</c>,
/// <c>csharp_find_references</c>, and <c>csharp_get_member_signatures</c>.
/// Uses a real <see cref="RoslynWorkspaceService"/> loaded against
/// <c>Fixtures/SampleProject/</c> (the same pattern as
/// <see cref="BuildDiagnosticEnricherTests"/>).
/// </summary>
public sealed class CSharpAnalysisToolsTests : IAsyncLifetime
{
    private readonly RoslynWorkspaceService _service = new(NullLogger<RoslynWorkspaceService>.Instance);
    private CSharpAnalysisTools _tools = null!;
    private string _fixtureDir = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(CSharpAnalysisToolsTests).Assembly.Location)!;
        _fixtureDir = Path.Combine(assemblyDir, "Fixtures", "SampleProject");
        var projectPath = Path.Combine(_fixtureDir, "SampleProject.csproj");
        await _service.InitializeAsync(projectPath, TestContext.Current.CancellationToken);
        _tools = new CSharpAnalysisTools(_service);
    }

    public ValueTask DisposeAsync()
    {
        _service.Dispose();
        return ValueTask.CompletedTask;
    }

    // ────── FindSymbol tests ──────

    [Fact]
    public async Task FindSymbol_ExactMatch_ReturnsClassLocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindSymbol("PlayerController", cancellationToken: ct);

        result.MatchCount.Should().BeGreaterThanOrEqualTo(1);
        result.Matches.Should().Contain(m =>
            m.Name == "PlayerController" && m.Kind == "Class" && m.Line.HasValue);
    }

    [Fact]
    public async Task FindSymbol_ContainsMatch_ReturnsMultipleSymbols()
    {
        var ct = TestContext.Current.CancellationToken;
        // "Item" should match AddItem, RemoveItem, HasItem, ItemAdded in Inventory.
        var result = await _tools.CSharpFindSymbol("Item", contains: true, cancellationToken: ct);

        result.MatchCount.Should().BeGreaterThanOrEqualTo(3);
        result.Query.Should().Be("Item");
    }

    [Fact]
    public async Task FindSymbol_KindFilter_ReturnsOnlyMethods()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindSymbol("TakeDamage", kind: "method", cancellationToken: ct);

        result.MatchCount.Should().BeGreaterThanOrEqualTo(1);
        result.Matches.Should().OnlyContain(m => m.Kind == "Method");
    }

    [Fact]
    public async Task FindSymbol_Limit_CapsResults()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindSymbol("Item", contains: true, limit: 2, cancellationToken: ct);

        result.MatchCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task FindSymbol_NoResults_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindSymbol("XyzNonexistentSymbol12345", cancellationToken: ct);

        result.MatchCount.Should().Be(0);
        result.Matches.Should().BeEmpty();
    }

    // ────── FindReferences tests ──────

    [Fact]
    public async Task FindReferences_ByNameOnly_ReturnsCallSites()
    {
        var ct = TestContext.Current.CancellationToken;
        // Die() is only declared in PlayerController (unambiguous) and called inside TakeDamage.
        var result = await _tools.CSharpFindReferences("Die", cancellationToken: ct);

        result.ReferenceCount.Should().BeGreaterThanOrEqualTo(1);
        result.ResolvedFullName.Should().NotBeNullOrEmpty();
        result.References.Should().Contain(r => r.EnclosingMethod == "TakeDamage");
    }

    [Fact]
    public async Task FindReferences_ByFileAndLine_PinsToSpecificSymbol()
    {
        var ct = TestContext.Current.CancellationToken;
        var playerController = Path.Combine(_fixtureDir, "PlayerController.cs");

        // TakeDamage is declared at line 13 in PlayerController.cs. When we pin to this
        // specific definition, the result should include the call in EnemyAI.Attack and
        // should NOT be flagged as ambiguous.
        var result = await _tools.CSharpFindReferences(
            "TakeDamage", filePath: playerController, line: 13, cancellationToken: ct);

        result.ReferenceCount.Should().BeGreaterThanOrEqualTo(1);
        result.WasAmbiguous.Should().BeFalse();
        result.References.Should().Contain(r => r.EnclosingMethod == "Attack");
    }

    [Fact]
    public async Task FindReferences_AmbiguousName_SetsWarningFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        // "TakeDamage" exists in both PlayerController and BrokenType — ambiguous by name.
        var result = await _tools.CSharpFindReferences("TakeDamage", cancellationToken: ct);

        result.WasAmbiguous.Should().BeTrue();
    }

    [Fact]
    public async Task FindReferences_Limit_CapsResults()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindReferences("TakeDamage", limit: 1, cancellationToken: ct);

        result.ReferenceCount.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task FindReferences_ZeroReferences_ReturnsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpFindReferences("XyzUnusedSymbol99999", cancellationToken: ct);

        result.ReferenceCount.Should().Be(0);
        result.References.Should().BeEmpty();
    }

    // ────── GetMemberSignatures tests ──────

    [Fact]
    public async Task GetMemberSignatures_SimpleNameResolution()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpGetMemberSignatures("Inventory", cancellationToken: ct);

        result.Should().NotBeNull();
        result!.Kind.Should().Be("class");
        result.Members.Should().Contain(m => m.Name == "AddItem" && m.Kind == "Method");
        result.Members.Should().Contain(m => m.Name == "Capacity" && m.Kind == "Property");
        result.Members.Should().Contain(m => m.Name == "ItemAdded" && m.Kind == "Event");
    }

    [Fact]
    public async Task GetMemberSignatures_FullyQualifiedName()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpGetMemberSignatures("SampleProject.Inventory", cancellationToken: ct);

        result.Should().NotBeNull();
        result!.TypeName.Should().Be("SampleProject.Inventory");
        result.Members.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMemberSignatures_NonExistentType_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _tools.CSharpGetMemberSignatures("NonExistentType12345", cancellationToken: ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberSignatures_IncludeInherited_AddsBaseMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var withInherited = await _tools.CSharpGetMemberSignatures(
            "EnemyAI", includeInherited: true, cancellationToken: ct);
        var without = await _tools.CSharpGetMemberSignatures(
            "EnemyAI", includeInherited: false, cancellationToken: ct);

        withInherited.Should().NotBeNull();
        without.Should().NotBeNull();
        withInherited!.Members.Count.Should().BeGreaterThanOrEqualTo(without!.Members.Count);
    }

    [Fact]
    public async Task GetMemberSignatures_IncludePrivate_ExposesPrivateMembers()
    {
        var ct = TestContext.Current.CancellationToken;
        var withPrivate = await _tools.CSharpGetMemberSignatures(
            "Inventory", includePrivate: true, cancellationToken: ct);
        var withoutPrivate = await _tools.CSharpGetMemberSignatures(
            "Inventory", includePrivate: false, cancellationToken: ct);

        withPrivate.Should().NotBeNull();
        withoutPrivate.Should().NotBeNull();
        withPrivate!.Members.Count.Should().BeGreaterThan(withoutPrivate!.Members.Count);
        withPrivate.Members.Should().Contain(m => m.Accessibility == "private");
    }
}
