using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Wick.Core;
using Wick.Server.Tools;

namespace Wick.Server;

/// <summary>
/// Introspection tools for the static tool group system. Always registered.
/// These tools report the resolved active group set from startup; they do NOT
/// mutate it. Runtime switching is a forward-compat placeholder (tool_reset).
/// </summary>
[McpServerToolType]
public static class ToolGroupTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [McpServerTool, Description(
        "Returns every known Wick tool with its group assignment and whether it is " +
        "active in this process. Use to discover available functionality.")]
    public static string ToolCatalog(McpServer server)
    {
        var active = GetActiveGroups(server);
        var all = DefaultToolGroups.All;

        var tools = all
            .SelectMany(g => g.Tools.Select(t => new
            {
                name = t,
                group = g.Name,
                active = g.IsCore || active.Contains(g.Name),
            }))
            .OrderBy(t => t.group)
            .ThenBy(t => t.name)
            .ToList();

        return JsonSerializer.Serialize(new { tools }, JsonOptions);
    }

    [McpServerTool, Description(
        "Returns the resolved active tool groups for this Wick process along with " +
        "descriptions of every available group.")]
    public static string ToolGroups(McpServer server)
    {
        var active = GetActiveGroups(server);
        var all = DefaultToolGroups.All;

        return JsonSerializer.Serialize(new
        {
            active_groups = active.Groups.OrderBy(g => g).ToList(),
            available_groups = all.Select(g => g.Name).OrderBy(n => n).ToList(),
            group_descriptions = all.ToDictionary(g => g.Name, g => g.Description),
        }, JsonOptions);
    }

    [McpServerTool, Description(
        "Placeholder for future dynamic group activation. Not functional in v1: " +
        "Wick resolves groups once at startup from --groups or WICK_GROUPS. " +
        "This tool is retained for forward compatibility.")]
    public static string ToolReset(McpServer server)
    {
        return JsonSerializer.Serialize(new
        {
            supported = false,
            reason = "Static startup configuration in v1. Restart Wick with --groups=... or WICK_GROUPS=... to change active groups.",
        }, JsonOptions);
    }

    private static ActiveGroups GetActiveGroups(McpServer server)
    {
        var provider = server.Services
            ?? throw new InvalidOperationException("McpServer.Services is null");
        return provider.GetRequiredService<ActiveGroups>();
    }
}
