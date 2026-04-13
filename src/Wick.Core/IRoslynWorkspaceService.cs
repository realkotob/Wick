namespace Wick.Core;

/// <summary>
/// Interface for the Roslyn workspace service, defined in Core to avoid circular deps.
/// Implemented by RoslynWorkspaceService in Wick.Providers.CSharp.
/// </summary>
public interface IRoslynWorkspaceService : IDisposable
{
    Task InitializeAsync(string projectOrSolutionPath, CancellationToken ct);
    SourceContext? GetSourceContext(string filePath, int line);
    Task<string[]> GetCallersAsync(string typeName, string methodName);
    bool IsLoaded { get; }
    string? LoadError { get; }

    /// <summary>
    /// For a "name does not exist" style diagnostic (CS0103, CS1061, CS0117), returns a short
    /// human-readable hint of near-miss symbol names visible at the given file/line. Returns
    /// null when the workspace cannot resolve the position, when no candidates are found,
    /// or when the implementation does not support signature hinting.
    /// </summary>
    string? GetSignatureHint(string filePath, int line, string missingName) => null;

    /// <summary>
    /// Searches the loaded workspace for declarations whose name matches <paramref name="name"/>.
    /// Filters by <paramref name="kind"/> ("type" | "method" | "property" | "field" | "event" | "any").
    /// When <paramref name="contains"/> is true, matches substring rather than exact. Caps
    /// results at <paramref name="limit"/>.
    /// Default implementation returns an empty list so existing test substitutes keep working.
    /// </summary>
    Task<IReadOnlyList<SymbolLocation>> FindSymbolsAsync(
        string name, string kind, bool contains, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SymbolLocation>>(Array.Empty<SymbolLocation>());

    /// <summary>
    /// Finds all references to the symbol named <paramref name="symbolName"/>. If
    /// <paramref name="filePath"/> and <paramref name="line"/> are supplied, pins to the
    /// specific symbol at that location; otherwise picks the lexicographically smallest
    /// full-name match and sets <see cref="FindReferencesResult.WasAmbiguous"/> when more
    /// than one candidate existed.
    /// Default implementation returns an empty result so existing test substitutes keep working.
    /// </summary>
    Task<FindReferencesResult> FindReferencesAsync(
        string symbolName, string? filePath, int? line, int limit, CancellationToken ct)
        => Task.FromResult(new FindReferencesResult(
            symbolName, null, false, 0, Array.Empty<SymbolReference>()));

    /// <summary>
    /// Returns the members (methods, properties, fields, events, constructors) of the
    /// type named <paramref name="typeName"/>, with signatures. Returns <c>null</c> when
    /// the type cannot be resolved in the loaded workspace source.
    /// Default implementation returns null so existing test substitutes keep working.
    /// </summary>
    Task<TypeMembersResult?> GetMemberSignaturesAsync(
        string typeName, bool includeInherited, bool includePrivate, CancellationToken ct)
        => Task.FromResult<TypeMembersResult?>(null);
}
