using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using Wick.Core;

namespace Wick.Providers.CSharp;

/// <summary>
/// Loads a .NET project into a Roslyn workspace and provides source-context queries
/// for the exception enrichment pipeline.
/// </summary>
public sealed partial class RoslynWorkspaceService : IRoslynWorkspaceService
{
    private readonly ILogger<RoslynWorkspaceService> _logger;
    private MSBuildWorkspace? _workspace;
    private Compilation? _compilation;
    private Project? _project;

    public RoslynWorkspaceService(ILogger<RoslynWorkspaceService> logger)
    {
        _logger = logger;
    }

    /// <summary>Gets a value indicating whether the project has been loaded successfully.</summary>
    public bool IsLoaded => _compilation is not null;

    /// <summary>Gets the error message if loading failed, or null on success.</summary>
    public string? LoadError { get; private set; }

    /// <summary>
    /// Loads the specified project or solution into the Roslyn workspace.
    /// Supports <c>.csproj</c>, <c>.sln</c>, and <c>.slnx</c> files.
    /// </summary>
    public async Task InitializeAsync(string projectOrSolutionPath, CancellationToken ct)
    {
        try
        {
            _workspace = MSBuildWorkspace.Create();
            var ext = Path.GetExtension(projectOrSolutionPath);

            if (ext.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var solution = await _workspace.OpenSolutionAsync(projectOrSolutionPath, cancellationToken: ct).ConfigureAwait(false);
                _project = solution.Projects.FirstOrDefault();
            }
            else
            {
                _project = await _workspace.OpenProjectAsync(projectOrSolutionPath, cancellationToken: ct).ConfigureAwait(false);
            }

            if (_project is not null)
            {
                _compilation = await _project.GetCompilationAsync(ct).ConfigureAwait(false);
            }

            if (_compilation is null)
            {
                LoadError = "Failed to produce a Compilation from the project.";
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
    }

    /// <summary>
    /// Returns source context for the method enclosing the given line in the specified file.
    /// Returns null if the file or method cannot be found.
    /// </summary>
    public SourceContext? GetSourceContext(string filePath, int line)
    {
        try
        {
            if (_compilation is null || _project is null)
            {
                return null;
            }

            var normalizedPath = Path.GetFullPath(filePath);
            var document = _project.Documents.FirstOrDefault(d =>
                d.FilePath is not null &&
                Path.GetFullPath(d.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (document is null)
            {
                return null;
            }

            var tree = _compilation.SyntaxTrees.FirstOrDefault(t =>
                t.FilePath is not null &&
                Path.GetFullPath(t.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (tree is null)
            {
                return null;
            }

            var root = tree.GetRoot();
            var text = tree.GetText();

            // line is 1-based; Roslyn uses 0-based
            var zeroLine = line - 1;
            if (zeroLine < 0 || zeroLine >= text.Lines.Count)
            {
                return null;
            }

            var position = text.Lines[zeroLine].Start;

            // Find the enclosing method
            var node = root.FindToken(position).Parent;
            var methodNode = node?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();

            if (methodNode is null)
            {
                return null;
            }

            // Method body
            var methodBody = methodNode.ToFullString().Trim();

            // Surrounding lines (±5)
            var methodStartLine = methodNode.GetLocation().GetLineSpan().StartLinePosition.Line;
            var methodEndLine = methodNode.GetLocation().GetLineSpan().EndLinePosition.Line;
            var surroundStart = Math.Max(0, methodStartLine - 5);
            var surroundEnd = Math.Min(text.Lines.Count - 1, methodEndLine + 5);
            var surroundingLines = string.Join(
                Environment.NewLine,
                Enumerable.Range(surroundStart, surroundEnd - surroundStart + 1)
                    .Select(i => text.Lines[i].ToString()));

            // Enclosing type with base types
            var typeNode = methodNode.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            string? enclosingType = null;
            if (typeNode is not null)
            {
                enclosingType = typeNode.Identifier.Text;
                if (typeNode.BaseList is not null)
                {
                    enclosingType += " : " + string.Join(", ", typeNode.BaseList.Types.Select(t => t.ToString()));
                }
            }

            // Nearest doc comment (XML documentation comment on the method)
            string? nearestComment = null;
            var trivia = methodNode.GetLeadingTrivia();
            var docComment = trivia
                .Where(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia))
                .LastOrDefault();

            if (docComment != default)
            {
                nearestComment = docComment.ToFullString().Trim();
            }

            return new SourceContext
            {
                MethodBody = methodBody,
                SurroundingLines = surroundingLines,
                EnclosingType = enclosingType,
                NearestComment = nearestComment,
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogSourceContextFailed(ex, filePath, line);
            return null;
        }
    }

    /// <summary>
    /// Finds all call sites for the specified method across the loaded project.
    /// Returns entries formatted as <c>"TypeName.MethodName (FileName.cs:lineNum)"</c>.
    /// </summary>
    public async Task<string[]> GetCallersAsync(string typeName, string methodName)
    {
        try
        {
            if (_compilation is null || _project is null)
            {
                return [];
            }

            return await GetCallersInternalAsync(typeName, methodName).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogGetCallersFailed(ex, typeName, methodName);
            return [];
        }
    }

    private async Task<string[]> GetCallersInternalAsync(string typeName, string methodName)
    {
        if (_compilation is null || _project is null)
        {
            return [];
        }

        // Find the target type
        var targetType = _compilation.GetSymbolsWithName(typeName, SymbolFilter.Type).OfType<INamedTypeSymbol>().FirstOrDefault();
        if (targetType is null)
        {
            return [];
        }

        // Find the target method
        var targetMethod = targetType.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault();
        if (targetMethod is null)
        {
            return [];
        }

        var solution = _project.Solution;
        var references = await SymbolFinder.FindReferencesAsync(targetMethod, solution).ConfigureAwait(false);

        var results = new List<string>();
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var refDoc = location.Document;
                var span = location.Location.SourceSpan;
                var refTree = await refDoc.GetSyntaxTreeAsync().ConfigureAwait(false);
                if (refTree is null)
                {
                    continue;
                }

                var refRoot = await refTree.GetRootAsync().ConfigureAwait(false);
                var refNode = refRoot.FindToken(span.Start).Parent;

                // Find enclosing method
                var enclosingMethod = refNode?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (enclosingMethod is null)
                {
                    continue;
                }

                var enclosingType = enclosingMethod.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                var callerTypeName = enclosingType?.Identifier.Text ?? "Unknown";
                var callerMethodName = enclosingMethod.Identifier.Text;
                var fileName = Path.GetFileName(refDoc.FilePath ?? "Unknown.cs");
                var lineNumber = refTree.GetLineSpan(span).StartLinePosition.Line + 1;

                results.Add($"{callerTypeName}.{callerMethodName} ({fileName}:{lineNumber})");
            }
        }

        return [.. results];
    }

    /// <summary>
    /// For a CS0103/CS1061/CS0117-style "name does not exist" error, returns a short
    /// comma-separated list of near-miss symbol names visible at the given position.
    /// Uses a Levenshtein-bounded candidate search. Returns null on any failure.
    /// </summary>
    public string? GetSignatureHint(string filePath, int line, string missingName)
    {
        try
        {
            if (_compilation is null || _project is null || string.IsNullOrEmpty(missingName))
            {
                return null;
            }

            var normalizedPath = Path.GetFullPath(filePath);
            var tree = _compilation.SyntaxTrees.FirstOrDefault(t =>
                t.FilePath is not null &&
                Path.GetFullPath(t.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (tree is null)
            {
                return null;
            }

            var text = tree.GetText();
            var zeroLine = line - 1;
            if (zeroLine < 0 || zeroLine >= text.Lines.Count)
            {
                return null;
            }

            var position = text.Lines[zeroLine].Start;
            var semanticModel = _compilation.GetSemanticModel(tree);
            var symbols = semanticModel.LookupSymbols(position);

            // Score by Levenshtein distance to the missing name. Take up to 3 best matches
            // within distance 3.
            var candidates = symbols
                .Select(s => s.Name)
                .Where(n => !string.IsNullOrEmpty(n) && !n.StartsWith('<'))
                .Distinct(StringComparer.Ordinal)
                .Select(n => (Name: n, Distance: Levenshtein(n, missingName)))
                .Where(x => x.Distance <= 3 && x.Distance > 0)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Take(3)
                .Select(x => x.Name)
                .ToArray();

            if (candidates.Length == 0)
            {
                return null;
            }

            return $"Did you mean: {string.Join(", ", candidates)}?";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogSignatureHintFailed(ex, missingName);
            return null;
        }
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            Array.Copy(curr, prev, b.Length + 1);
        }
        return prev[b.Length];
    }

    /// <summary>
    /// Searches the workspace for declarations matching <paramref name="name"/>, filtered
    /// by kind. Uses <see cref="SymbolFinder.FindDeclarationsAsync(Project, string, bool, SymbolFilter, CancellationToken)"/>
    /// across all projects in the solution.
    /// </summary>
    public async Task<IReadOnlyList<SymbolLocation>> FindSymbolsAsync(
        string name, string kind, bool contains, int limit, CancellationToken ct)
    {
        if (_project is null || string.IsNullOrEmpty(name))
        {
            return Array.Empty<SymbolLocation>();
        }

        var normalizedKind = (kind ?? "any").ToLowerInvariant();
        var filter = normalizedKind switch
        {
            "type" => SymbolFilter.Type,
            "method" or "property" or "field" or "event" or "constructor" => SymbolFilter.Member,
            _ => SymbolFilter.TypeAndMember,
        };

        var solution = _project.Solution;
        var results = new List<SymbolLocation>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            IEnumerable<ISymbol> found;
            try
            {
                // FindDeclarationsAsync supports a direct name match; for "contains" we fall
                // back to scanning the compilation's global namespace. Keep it simple: run the
                // exact-name API and then, if contains is on, also walk all types/members.
                found = await SymbolFinder.FindDeclarationsAsync(
                    project, name, ignoreCase: false, filter, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogProjectSkipped(ex, project.Name, "symbol search");
                continue;
            }

            if (contains)
            {
                // Augment with substring matches by walking the compilation.
                var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
                if (compilation is not null)
                {
                    var extra = new List<ISymbol>();
                    CollectMatchingSymbols(compilation.GlobalNamespace, name, filter, extra);
                    found = found.Concat(extra);
                }
            }

            foreach (var symbol in found)
            {
                if (!MatchesKindFilter(symbol, normalizedKind))
                {
                    continue;
                }

                var location = ToSymbolLocation(symbol);
                if (location is null)
                {
                    continue;
                }

                var dedupeKey = location.FullName + "|" + (location.FilePath ?? "") + "|" + (location.Line ?? 0);
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                results.Add(location);
                if (results.Count >= limit)
                {
                    return results;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Finds all references to a symbol, optionally disambiguated by file+line.
    /// </summary>
    public async Task<FindReferencesResult> FindReferencesAsync(
        string symbolName, string? filePath, int? line, int limit, CancellationToken ct)
    {
        if (_project is null || string.IsNullOrEmpty(symbolName))
        {
            return new FindReferencesResult(symbolName ?? "", null, false, 0, Array.Empty<SymbolReference>());
        }

        var solution = _project.Solution;
        ISymbol? target = null;
        bool wasAmbiguous = false;

        if (!string.IsNullOrEmpty(filePath) && line.HasValue)
        {
            target = await ResolveSymbolAtLocationAsync(filePath, line.Value, symbolName, ct).ConfigureAwait(false);
        }

        if (target is null)
        {
            // Name-only resolution: gather all matching declarations and pick the
            // lexicographically smallest full name. Report ambiguity when >1 match.
            var matches = new List<ISymbol>();
            foreach (var project in solution.Projects)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var found = await SymbolFinder.FindDeclarationsAsync(
                        project, symbolName, ignoreCase: false, SymbolFilter.TypeAndMember, ct).ConfigureAwait(false);
                    matches.AddRange(found);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    LogProjectSkipped(ex, project.Name, "reference search");
                }
            }

            if (matches.Count == 0)
            {
                return new FindReferencesResult(symbolName, null, false, 0, Array.Empty<SymbolReference>());
            }

            matches.Sort((a, b) => string.CompareOrdinal(
                a.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                b.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            target = matches[0];
            wasAmbiguous = matches.Count > 1;
        }

        if (target is null)
        {
            return new FindReferencesResult(symbolName, null, false, 0, Array.Empty<SymbolReference>());
        }

        var references = new List<SymbolReference>();
        try
        {
            var refs = await SymbolFinder.FindReferencesAsync(target, solution, ct).ConfigureAwait(false);
            foreach (var referencedSymbol in refs)
            {
                foreach (var loc in referencedSymbol.Locations)
                {
                    ct.ThrowIfCancellationRequested();
                    if (references.Count >= limit)
                    {
                        break;
                    }

                    var refDoc = loc.Document;
                    var span = loc.Location.SourceSpan;
                    var refTree = await refDoc.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
                    if (refTree is null)
                    {
                        continue;
                    }

                    var lineSpan = refTree.GetLineSpan(span, ct);
                    var refText = await refTree.GetTextAsync(ct).ConfigureAwait(false);
                    var oneBasedLine = lineSpan.StartLinePosition.Line + 1;
                    var oneBasedCol = lineSpan.StartLinePosition.Character + 1;

                    string? lineText = null;
                    if (lineSpan.StartLinePosition.Line < refText.Lines.Count)
                    {
                        lineText = refText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
                    }

                    string? enclosingMethod = null;
                    var refRoot = await refTree.GetRootAsync(ct).ConfigureAwait(false);
                    var node = refRoot.FindToken(span.Start).Parent;
                    var methodNode = node?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    enclosingMethod = methodNode?.Identifier.Text;

                    references.Add(new SymbolReference(
                        FilePath: refDoc.FilePath ?? string.Empty,
                        Line: oneBasedLine,
                        Column: oneBasedCol,
                        LineText: lineText,
                        EnclosingMethod: enclosingMethod));
                }

                if (references.Count >= limit)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogReferenceEnumerationFailed(ex, symbolName);
        }

        var resolvedFullName = target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return new FindReferencesResult(
            SymbolName: symbolName,
            ResolvedFullName: resolvedFullName,
            WasAmbiguous: wasAmbiguous,
            ReferenceCount: references.Count,
            References: references);
    }

    /// <summary>
    /// Returns the members of a type with signatures. Resolves by fully-qualified
    /// metadata name first, then falls back to <see cref="SymbolFinder.FindDeclarationsAsync(Project, string, bool, SymbolFilter, CancellationToken)"/>
    /// for simple names.
    /// </summary>
    public async Task<TypeMembersResult?> GetMemberSignaturesAsync(
        string typeName, bool includeInherited, bool includePrivate, CancellationToken ct)
    {
        if (_compilation is null || _project is null || string.IsNullOrEmpty(typeName))
        {
            return null;
        }

        INamedTypeSymbol? typeSymbol = null;
        if (typeName.Contains('.', StringComparison.Ordinal))
        {
            typeSymbol = _compilation.GetTypeByMetadataName(typeName);
        }

        if (typeSymbol is null)
        {
            // Fallback: find by simple name across the solution.
            foreach (var project in _project.Solution.Projects)
            {
                ct.ThrowIfCancellationRequested();
                IEnumerable<ISymbol> found;
                try
                {
                    found = await SymbolFinder.FindDeclarationsAsync(
                        project, typeName, ignoreCase: false, SymbolFilter.Type, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    LogProjectSkipped(ex, project.Name, "member signature lookup");
                    continue;
                }

                typeSymbol = found.OfType<INamedTypeSymbol>().FirstOrDefault();
                if (typeSymbol is not null)
                {
                    break;
                }
            }
        }

        if (typeSymbol is null)
        {
            return null;
        }

        // Out of scope: symbols defined in referenced assemblies (no source locations in workspace).
        var definedIn = typeSymbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
        if (definedIn is null)
        {
            return null;
        }

        var signatureFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            memberOptions: SymbolDisplayMemberOptions.IncludeParameters
                | SymbolDisplayMemberOptions.IncludeType
                | SymbolDisplayMemberOptions.IncludeModifiers
                | SymbolDisplayMemberOptions.IncludeAccessibility
                | SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeRef,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType
                | SymbolDisplayParameterOptions.IncludeName
                | SymbolDisplayParameterOptions.IncludeParamsRefOut
                | SymbolDisplayParameterOptions.IncludeDefaultValue,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        IEnumerable<ISymbol> memberSymbols = includeInherited
            ? CollectInheritedMembers(typeSymbol)
            : typeSymbol.GetMembers();

        var members = new List<MemberSignature>();
        foreach (var member in memberSymbols)
        {
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            // Skip compiler-generated property backing fields / accessors.
            if (member is IMethodSymbol methodSkip && (
                methodSkip.MethodKind == MethodKind.PropertyGet
                || methodSkip.MethodKind == MethodKind.PropertySet
                || methodSkip.MethodKind == MethodKind.EventAdd
                || methodSkip.MethodKind == MethodKind.EventRemove))
            {
                continue;
            }

            if (!includePrivate && member.DeclaredAccessibility == Accessibility.Private)
            {
                continue;
            }

            var kindString = member.Kind switch
            {
                SymbolKind.Method => ((IMethodSymbol)member).MethodKind == MethodKind.Constructor ? "Constructor" : "Method",
                SymbolKind.Property => "Property",
                SymbolKind.Field => "Field",
                SymbolKind.Event => "Event",
                _ => member.Kind.ToString(),
            };

            var signature = member.ToDisplayString(signatureFormat);
            var accessibility = member.DeclaredAccessibility switch
            {
                Accessibility.Public => "public",
                Accessibility.Internal => "internal",
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedAndInternal => "private protected",
                Accessibility.ProtectedOrInternal => "protected internal",
                _ => "unknown",
            };

            int? memberLine = null;
            var memberLoc = member.Locations.FirstOrDefault(l => l.IsInSource);
            if (memberLoc is not null)
            {
                memberLine = memberLoc.GetLineSpan().StartLinePosition.Line + 1;
            }

            members.Add(new MemberSignature(
                Name: member.Name,
                Kind: kindString,
                Signature: signature,
                Accessibility: accessibility,
                IsStatic: member.IsStatic,
                Line: memberLine));
        }

        var typeKind = typeSymbol.TypeKind switch
        {
            TypeKind.Class => typeSymbol.IsRecord ? "record" : "class",
            TypeKind.Struct => typeSymbol.IsRecord ? "record struct" : "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => typeSymbol.TypeKind.ToString().ToLowerInvariant(),
        };

        var baseType = typeSymbol.BaseType is { SpecialType: SpecialType.System_Object } or null
            ? null
            : typeSymbol.BaseType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        var interfaces = typeSymbol.Interfaces
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
            .ToArray();

        return new TypeMembersResult(
            TypeName: typeName,
            ResolvedFullName: typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            DefinedIn: definedIn,
            Kind: typeKind,
            BaseType: baseType,
            Interfaces: interfaces,
            Members: members);
    }

    // ------------------ private helpers for Sub-spec D ------------------

    private static IEnumerable<ISymbol> CollectInheritedMembers(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                // For inherited walks, avoid yielding the same override twice.
                var key = member.Kind + "|" + member.Name + "|" + member.ToDisplayString();
                if (seen.Add(key))
                {
                    yield return member;
                }
            }
        }
    }

    private static bool MatchesKindFilter(ISymbol symbol, string normalizedKind) => normalizedKind switch
    {
        "any" => true,
        "type" => symbol is INamedTypeSymbol,
        "method" => symbol is IMethodSymbol m && m.MethodKind != MethodKind.PropertyGet && m.MethodKind != MethodKind.PropertySet,
        "property" => symbol is IPropertySymbol,
        "field" => symbol is IFieldSymbol,
        "event" => symbol is IEventSymbol,
        "constructor" => symbol is IMethodSymbol mc && mc.MethodKind == MethodKind.Constructor,
        _ => true,
    };

    private static SymbolLocation? ToSymbolLocation(ISymbol symbol)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        string? filePath = null;
        int? line = null;
        int? column = null;
        if (loc is not null)
        {
            var lineSpan = loc.GetLineSpan();
            filePath = lineSpan.Path;
            line = lineSpan.StartLinePosition.Line + 1;
            column = lineSpan.StartLinePosition.Character + 1;
        }

        var kind = symbol switch
        {
            INamedTypeSymbol t => t.TypeKind switch
            {
                TypeKind.Class => t.IsRecord ? "Record" : "Class",
                TypeKind.Struct => t.IsRecord ? "RecordStruct" : "Struct",
                TypeKind.Interface => "Interface",
                TypeKind.Enum => "Enum",
                TypeKind.Delegate => "Delegate",
                _ => "Type",
            },
            IMethodSymbol m => m.MethodKind == MethodKind.Constructor ? "Constructor" : "Method",
            IPropertySymbol => "Property",
            IFieldSymbol => "Field",
            IEventSymbol => "Event",
            _ => symbol.Kind.ToString(),
        };

        string? enclosingType = null;
        if (symbol.ContainingType is { } ct2)
        {
            enclosingType = ct2.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        return new SymbolLocation(
            Name: symbol.Name,
            Kind: kind,
            FullName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            FilePath: filePath,
            Line: line,
            Column: column,
            EnclosingType: enclosingType);
    }

    private static void CollectMatchingSymbols(
        INamespaceOrTypeSymbol container,
        string substring,
        SymbolFilter filter,
        List<ISymbol> sink)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol ns)
            {
                CollectMatchingSymbols(ns, substring, filter, sink);
                continue;
            }

            if (member is INamedTypeSymbol type)
            {
                if ((filter & SymbolFilter.Type) != 0 && type.Name.Contains(substring, StringComparison.Ordinal))
                {
                    sink.Add(type);
                }

                if ((filter & SymbolFilter.Member) != 0)
                {
                    foreach (var typeMember in type.GetMembers())
                    {
                        if (typeMember.Kind is SymbolKind.Method or SymbolKind.Property or SymbolKind.Field or SymbolKind.Event
                            && typeMember.Name.Contains(substring, StringComparison.Ordinal))
                        {
                            sink.Add(typeMember);
                        }
                    }
                }

                // Recurse for nested types
                CollectMatchingSymbols(type, substring, filter, sink);
            }
        }
    }

    private async Task<ISymbol?> ResolveSymbolAtLocationAsync(
        string filePath, int line, string expectedName, CancellationToken ct)
    {
        if (_compilation is null || _project is null)
        {
            return null;
        }

        var normalizedPath = Path.GetFullPath(filePath);
        var tree = _compilation.SyntaxTrees.FirstOrDefault(t =>
            t.FilePath is not null &&
            Path.GetFullPath(t.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (tree is null)
        {
            return null;
        }

        var text = await tree.GetTextAsync(ct).ConfigureAwait(false);
        var zeroLine = line - 1;
        if (zeroLine < 0 || zeroLine >= text.Lines.Count)
        {
            return null;
        }

        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        var lineText = text.Lines[zeroLine];
        var model = _compilation.GetSemanticModel(tree);

        // Walk tokens in this line looking for a declaration identifier that matches expectedName.
        var lineStart = lineText.Start;
        var lineEnd = lineText.End;

        foreach (var token in root.DescendantTokens()
            .Where(t => t.SpanStart >= lineStart && t.SpanStart <= lineEnd))
        {
            if (!token.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierToken))
            {
                continue;
            }

            if (!string.Equals(token.Text, expectedName, StringComparison.Ordinal))
            {
                continue;
            }

            var node = token.Parent;
            if (node is null)
            {
                continue;
            }

            var declared = model.GetDeclaredSymbol(node, ct);
            if (declared is not null && string.Equals(declared.Name, expectedName, StringComparison.Ordinal))
            {
                return declared;
            }

            // Walk up to the containing declaration node (method/property/etc.)
            foreach (var ancestor in node.AncestorsAndSelf())
            {
                var ancestorSymbol = model.GetDeclaredSymbol(ancestor, ct);
                if (ancestorSymbol is not null && string.Equals(ancestorSymbol.Name, expectedName, StringComparison.Ordinal))
                {
                    return ancestorSymbol;
                }
            }
        }

        return null;
    }

    /// <summary>Disposes the underlying MSBuild workspace.</summary>
    public void Dispose()
    {
        _workspace?.Dispose();
    }

    // ------------------ LoggerMessage source-gen declarations ------------------

    [LoggerMessage(EventId = 200, Level = LogLevel.Debug,
        Message = "Failed to get source context for {FilePath}:{Line}")]
    private partial void LogSourceContextFailed(Exception ex, string filePath, int line);

    [LoggerMessage(EventId = 201, Level = LogLevel.Debug,
        Message = "Failed to get callers for {Type}.{Method}")]
    private partial void LogGetCallersFailed(Exception ex, string type, string method);

    [LoggerMessage(EventId = 202, Level = LogLevel.Debug,
        Message = "Signature hint lookup failed for {Name}")]
    private partial void LogSignatureHintFailed(Exception ex, string name);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug,
        Message = "Skipping project {Project} during {Operation}")]
    private partial void LogProjectSkipped(Exception ex, string project, string operation);

    [LoggerMessage(EventId = 204, Level = LogLevel.Debug,
        Message = "Error enumerating references for {Symbol}")]
    private partial void LogReferenceEnumerationFailed(Exception ex, string symbol);
}
