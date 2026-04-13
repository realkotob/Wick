namespace SampleProject;

/// <summary>
/// Intentionally uncompilable fixture used by BuildDiagnosticEnricher tests.
/// NOTE: The csproj <c>&lt;Compile Remove="Fixtures/**/*.cs" /&gt;</c> entry in
/// Wick.Tests.Unit.csproj prevents this file from being compiled as part of
/// the test assembly. It exists purely for Roslyn workspace loading to
/// produce a predictable CS0103 diagnostic at a known line.
/// </summary>
public class BrokenType
{
    public int Health { get; set; } = 100;

    public void TakeDamage(int amount)
    {
        // Line 15: References a name that does not exist in scope. CS0103 target.
        Healt -= amount;
    }
}
