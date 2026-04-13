using System.Text.RegularExpressions;
using Wick.Core;

namespace Wick.Providers.CSharp;

/// <summary>
/// Takes a parsed <see cref="BuildDiagnostic"/> and returns an enriched copy with
/// <see cref="BuildDiagnostic.Source"/> populated from the Roslyn workspace.
/// Mirrors the runtime-exception pattern of <see cref="ExceptionEnricher"/>:
/// every step is best-effort and failures collapse to <c>Source = null</c> on the
/// original diagnostic — the raw diagnostic always flows through unchanged.
/// </summary>
public sealed class BuildDiagnosticEnricher
{
    private static readonly HashSet<string> SignatureHintCodes = new(StringComparer.Ordinal)
    {
        "CS0103", // The name 'X' does not exist in the current context
        "CS1061", // 'T' does not contain a definition for 'X'
        "CS0117", // 'T' does not contain a definition for 'X' (static)
    };

    // Extracts the quoted missing name from a CS0103-style message:
    //   "The name 'Foo' does not exist..."
    //   "'Bar' does not contain a definition for 'Baz'..." — we pick the last quoted token for CS1061/CS0117
    private static readonly Regex QuotedNameRegex = new(
        @"'([^']+)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IRoslynWorkspaceService _workspace;

    public BuildDiagnosticEnricher(IRoslynWorkspaceService workspace)
    {
        _workspace = workspace;
    }

    /// <summary>
    /// Returns an enriched copy of <paramref name="diagnostic"/>. If the workspace is
    /// unloaded, the file is outside the workspace, or any step fails, returns the
    /// original diagnostic (with <see cref="BuildDiagnostic.Source"/> = null).
    /// </summary>
    public BuildDiagnostic Enrich(BuildDiagnostic diagnostic)
    {
        if (diagnostic.FilePath is null || diagnostic.Line is null)
        {
            return diagnostic;
        }

        if (!_workspace.IsLoaded)
        {
            return diagnostic;
        }

        SourceContext? runtimeContext;
        try
        {
            runtimeContext = _workspace.GetSourceContext(diagnostic.FilePath, diagnostic.Line.Value);
        }
        catch
        {
            return diagnostic;
        }

        string? signatureHint = null;
        if (SignatureHintCodes.Contains(diagnostic.Code))
        {
            var missingName = ExtractMissingName(diagnostic.Code, diagnostic.Message);
            if (!string.IsNullOrEmpty(missingName))
            {
                try
                {
                    signatureHint = _workspace.GetSignatureHint(diagnostic.FilePath, diagnostic.Line.Value, missingName);
                }
                catch
                {
                    signatureHint = null;
                }
            }
        }

        if (runtimeContext is null && signatureHint is null)
        {
            return diagnostic;
        }

        var buildSource = new BuildSourceContext
        {
            MethodBody = runtimeContext?.MethodBody,
            SurroundingLines = runtimeContext?.SurroundingLines,
            EnclosingType = runtimeContext?.EnclosingType,
            SignatureHint = signatureHint,
        };

        return diagnostic with { Source = buildSource };
    }

    /// <summary>
    /// Enriches a list of diagnostics, returning a new list. Best-effort; exceptions per
    /// diagnostic fall back to the original entry.
    /// </summary>
    public IReadOnlyList<BuildDiagnostic> EnrichAll(IReadOnlyList<BuildDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return diagnostics;
        }

        var result = new List<BuildDiagnostic>(diagnostics.Count);
        foreach (var d in diagnostics)
        {
            try
            {
                result.Add(Enrich(d));
            }
            catch
            {
                result.Add(d);
            }
        }
        return result;
    }

    private static string? ExtractMissingName(string code, string message)
    {
        var matches = QuotedNameRegex.Matches(message);
        if (matches.Count == 0)
        {
            return null;
        }

        // CS0103 "The name 'Foo' does not exist" → first quoted token is the missing name.
        // CS1061 "'T' does not contain a definition for 'Foo'" → last quoted token is the missing member.
        // CS0117 same shape as CS1061.
        return code switch
        {
            "CS0103" => matches[0].Groups[1].Value,
            "CS1061" => matches[^1].Groups[1].Value,
            "CS0117" => matches[^1].Groups[1].Value,
            _ => matches[0].Groups[1].Value,
        };
    }
}
