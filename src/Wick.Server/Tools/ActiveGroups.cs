using System.Collections.Immutable;

namespace Wick.Server.Tools;

/// <summary>
/// DI-injected snapshot of which tool groups are active for the lifetime of this process.
/// Populated once at startup by <see cref="ToolGroupResolver"/>.
/// </summary>
public sealed record ActiveGroups(ImmutableHashSet<string> Groups)
{
    public bool Contains(string group) => Groups.Contains(group);
}
