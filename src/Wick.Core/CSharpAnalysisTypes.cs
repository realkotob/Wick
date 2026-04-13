namespace Wick.Core;

/// <summary>
/// Public MCP response types for the Sub-spec D C# analysis tools
/// (<c>csharp_find_symbol</c>, <c>csharp_find_references</c>,
/// <c>csharp_get_member_signatures</c>). These live in <c>Wick.Core</c>
/// because they're on the public tool surface and the
/// <see cref="IRoslynWorkspaceService"/> interface needs to return them.
/// </summary>

/// <summary>
/// A single symbol location returned by <c>csharp_find_symbol</c> /
/// <see cref="IRoslynWorkspaceService.FindSymbolsAsync"/>.
/// </summary>
public sealed record SymbolLocation(
    string Name,
    string Kind,
    string FullName,
    string? FilePath,
    int? Line,
    int? Column,
    string? EnclosingType);

/// <summary>
/// Result container for <c>csharp_find_symbol</c>.
/// </summary>
public sealed record FindSymbolResult(
    string Query,
    string Kind,
    int MatchCount,
    IReadOnlyList<SymbolLocation> Matches);

/// <summary>
/// A single reference / usage location returned by <c>csharp_find_references</c>.
/// </summary>
public sealed record SymbolReference(
    string FilePath,
    int Line,
    int Column,
    string? LineText,
    string? EnclosingMethod);

/// <summary>
/// Result container for <c>csharp_find_references</c>. When multiple symbols
/// share <see cref="SymbolName"/> and the caller didn't disambiguate via
/// file+line, <see cref="WasAmbiguous"/> is true and the lexicographically
/// smallest full name was picked.
/// </summary>
public sealed record FindReferencesResult(
    string SymbolName,
    string? ResolvedFullName,
    bool WasAmbiguous,
    int ReferenceCount,
    IReadOnlyList<SymbolReference> References);

/// <summary>
/// A single member signature returned by <c>csharp_get_member_signatures</c>.
/// </summary>
public sealed record MemberSignature(
    string Name,
    string Kind,
    string Signature,
    string Accessibility,
    bool IsStatic,
    int? Line);

/// <summary>
/// Result container for <c>csharp_get_member_signatures</c>. Returned as
/// null when the type cannot be resolved in the loaded workspace source
/// (symbols defined in referenced assemblies are out of scope).
/// </summary>
public sealed record TypeMembersResult(
    string TypeName,
    string? ResolvedFullName,
    string? DefinedIn,
    string Kind,
    string? BaseType,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<MemberSignature> Members);
