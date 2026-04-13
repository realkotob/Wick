using System.Collections.Generic;

namespace Wick.Runtime.Bridge;

/// <summary>
/// Abstraction over "poke the Godot scene tree". The default implementation uses reflection
/// to find Godot's C# types at runtime (so <c>Wick.Runtime</c> does not need a build-time
/// reference to <c>GodotSharp</c>). Unit tests inject a stub.
/// </summary>
public interface ISceneBridge
{
    SceneNodeInfo GetSceneTree(int maxDepth);
    IReadOnlyDictionary<string, object?> GetNodeProperties(string nodePath);
    object? CallMethod(string nodePath, string method, IReadOnlyList<object?> args);
    void SetProperty(string nodePath, string propertyName, object? value);
    IReadOnlyList<SceneNodeInfo> FindNodesInGroup(string group);
}

/// <summary>Recursive, shallow-cloneable description of a scene node.</summary>
public sealed record SceneNodeInfo(
    string Name,
    string Type,
    string Path,
    IReadOnlyList<SceneNodeInfo> Children);

/// <summary>
/// Thrown by <see cref="ISceneBridge"/> when the scene tree cannot find the requested node.
/// The bridge server translates this into a structured <c>node_not_found</c> error.
/// </summary>
public sealed class SceneBridgeNodeNotFoundException : System.Exception
{
    public SceneBridgeNodeNotFoundException(string nodePath)
        : base($"Node not found: {nodePath}") { }
}

/// <summary>
/// Thrown when a method/property lookup fails. Distinguished from node-not-found because
/// the node exists but the member does not.
/// </summary>
public sealed class SceneBridgeMemberNotFoundException : System.Exception
{
    public string Kind { get; }
    public SceneBridgeMemberNotFoundException(string kind, string member)
        : base($"{kind} not found: {member}")
    {
        Kind = kind;
    }
}
