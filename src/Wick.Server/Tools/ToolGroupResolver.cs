using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Wick.Server.Tools;

/// <summary>
/// Resolves which tool groups are active for this process by combining
/// CLI flag precedence over env var, with unknown names logged and skipped.
/// Called once at startup; the result is captured in <see cref="ActiveGroups"/>.
/// </summary>
public static partial class ToolGroupResolver
{
    public static readonly ImmutableHashSet<string> KnownGroups =
        ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
            "core", "runtime", "scene", "csharp", "build");

    private static readonly ImmutableHashSet<string> CoreOnly =
        ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "core");

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Unknown tool group '{Group}' requested, skipping. Known groups: {Known}")]
    private static partial void LogUnknownGroup(ILogger logger, string group, string known);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No known tool groups resolved, falling back to 'core' only.")]
    private static partial void LogFallbackToCore(ILogger logger);

    public static ImmutableHashSet<string> Resolve(
        string? cliFlag,
        string? envValue,
        ILogger logger)
    {
        var source = !string.IsNullOrWhiteSpace(cliFlag) ? cliFlag : envValue;
        if (string.IsNullOrWhiteSpace(source))
        {
            return CoreOnly;
        }

        var requested = source
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToList();

        if (requested.Any(r => r == "all"))
        {
            return KnownGroups;
        }

        var resolved = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in requested)
        {
            if (KnownGroups.Contains(name))
            {
                resolved.Add(name);
            }
            else
            {
                LogUnknownGroup(logger, name, string.Join(",", KnownGroups.OrderBy(g => g)));
            }
        }

        if (resolved.Count == 0)
        {
            LogFallbackToCore(logger);
            return CoreOnly;
        }

        return resolved.ToImmutable();
    }
}
