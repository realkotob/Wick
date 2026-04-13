using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Wick.Runtime.Bridge;

/// <summary>
/// <see cref="ISceneBridge"/> that looks Godot's types up via reflection at runtime.
/// This keeps <c>Wick.Runtime</c> free of a build-time <c>GodotSharp</c> dependency
/// (which simplifies NuGet packaging and unit testing) while still working when hosted
/// inside an actual Godot 4.x C# game: <c>Engine.GetMainLoop()</c> returns the
/// <c>SceneTree</c>, we walk <c>GetRoot()</c>, and invoke instance members via
/// <see cref="MethodInfo.Invoke(object, object[])"/>.
///
/// Thread-safety: callers are responsible for marshalling invocations onto the Godot main
/// thread. See <see cref="MainThreadDispatcher"/> and <see cref="WickBridgeHandlers"/> for
/// the pattern. Calling this bridge directly from a background thread will usually crash
/// Godot; the tests use a stub implementation instead.
/// </summary>
internal sealed class ReflectionSceneBridge : ISceneBridge
{
    private const int MaxDepthCap = 50;

    private static readonly Lazy<(Type? engine, Type? node, Type? sceneTree)> s_godotTypes =
        new(ResolveGodotTypes);

    private static (Type? engine, Type? node, Type? sceneTree) ResolveGodotTypes()
    {
        Type? engine = null;
        Type? node = null;
        Type? sceneTree = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            engine ??= asm.GetType("Godot.Engine");
            node ??= asm.GetType("Godot.Node");
            sceneTree ??= asm.GetType("Godot.SceneTree");
            if (engine is not null && node is not null && sceneTree is not null)
            {
                break;
            }
        }
        return (engine, node, sceneTree);
    }

    private static object GetSceneTreeOrThrow()
    {
        var (engine, _, _) = s_godotTypes.Value;
        if (engine is null)
        {
            throw new NotSupportedException(
                "Godot C# runtime not loaded in this AppDomain. ReflectionSceneBridge " +
                "requires Godot.Engine / Godot.SceneTree to be resolvable at runtime.");
        }
        var getMainLoop = engine.GetMethod("GetMainLoop", BindingFlags.Public | BindingFlags.Static);
        var mainLoop = getMainLoop?.Invoke(null, null)
            ?? throw new InvalidOperationException("Engine.GetMainLoop() returned null.");
        return mainLoop;
    }

    private static object GetRootNodeOrThrow()
    {
        var mainLoop = GetSceneTreeOrThrow();
        var getRoot = mainLoop.GetType().GetMethod("GetRoot", BindingFlags.Public | BindingFlags.Instance);
        return getRoot?.Invoke(mainLoop, null)
            ?? throw new InvalidOperationException("SceneTree.GetRoot() returned null.");
    }

    private static object FindNodeOrThrow(string nodePath)
    {
        var root = GetRootNodeOrThrow();
        // Node.GetNode(NodePath) — we pass a string; Godot has a string overload.
        var nodeType = root.GetType();
        var getNode = nodeType.GetMethod("GetNode", [typeof(string)])
            ?? nodeType.GetMethod("GetNodeOrNull", [typeof(string)]);
        var result = getNode?.Invoke(root, [nodePath]);
        if (result is null)
        {
            throw new SceneBridgeNodeNotFoundException(nodePath);
        }
        return result;
    }

    public SceneNodeInfo GetSceneTree(int maxDepth)
    {
        var depth = Math.Clamp(maxDepth, 1, MaxDepthCap);
        var root = GetRootNodeOrThrow();
        return WalkNode(root, depth);
    }

    public IReadOnlyDictionary<string, object?> GetNodeProperties(string nodePath)
    {
        var node = FindNodeOrThrow(nodePath);
        var nodeType = node.GetType();
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }
            try
            {
                dict[prop.Name] = prop.GetValue(node)?.ToString();
            }
            catch
            {
                // Skip properties that blow up on Get (Godot proxies routinely do).
            }
        }
        return dict;
    }

    public object? CallMethod(string nodePath, string method, IReadOnlyList<object?> args)
    {
        var node = FindNodeOrThrow(nodePath);
        var nodeType = node.GetType();
        // Try Godot's universal Call(StringName, params Variant[]) first; fall back to CLR reflection.
        var callMethod = nodeType.GetMethod("Call", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), typeof(object[])], null);
        if (callMethod is not null)
        {
            return callMethod.Invoke(node, [method, args.ToArray()]);
        }

        var mi = nodeType.GetMethod(method, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new SceneBridgeMemberNotFoundException("method", method);
        return mi.Invoke(node, args.ToArray());
    }

    public void SetProperty(string nodePath, string propertyName, object? value)
    {
        var node = FindNodeOrThrow(nodePath);
        var nodeType = node.GetType();
        var pi = nodeType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (pi is null)
        {
            throw new SceneBridgeMemberNotFoundException("property", propertyName);
        }
        pi.SetValue(node, value);
    }

    public IReadOnlyList<SceneNodeInfo> FindNodesInGroup(string group)
    {
        var mainLoop = GetSceneTreeOrThrow();
        var mi = mainLoop.GetType().GetMethod("GetNodesInGroup", [typeof(string)]);
        var result = mi?.Invoke(mainLoop, [group]);
        if (result is null)
        {
            return Array.Empty<SceneNodeInfo>();
        }
        var list = new List<SceneNodeInfo>();
        if (result is System.Collections.IEnumerable enumerable)
        {
            foreach (var node in enumerable)
            {
                if (node is not null)
                {
                    list.Add(WalkNode(node, maxDepth: 1));
                }
            }
        }
        return list;
    }

    private static SceneNodeInfo WalkNode(object node, int maxDepth)
    {
        var nodeType = node.GetType();
        var name = TryGetString(node, nodeType, "Name") ?? "<unnamed>";
        var path = TryGetString(node, nodeType, "GetPath") ?? name;
        var children = new List<SceneNodeInfo>();
        if (maxDepth > 1)
        {
            var getChildCount = nodeType.GetMethod("GetChildCount", [])
                ?? nodeType.GetMethod("GetChildCount", [typeof(bool)]);
            var getChild = nodeType.GetMethod("GetChild", [typeof(int)])
                ?? nodeType.GetMethod("GetChild", [typeof(int), typeof(bool)]);
            if (getChildCount is not null && getChild is not null)
            {
                int count = 0;
                try
                {
                    var raw = getChildCount.GetParameters().Length == 0
                        ? getChildCount.Invoke(node, null)
                        : getChildCount.Invoke(node, [false]);
                    count = raw is int i ? i : 0;
                }
                catch { count = 0; }

                for (int i = 0; i < count; i++)
                {
                    object? child;
                    try
                    {
                        child = getChild.GetParameters().Length == 1
                            ? getChild.Invoke(node, [i])
                            : getChild.Invoke(node, [i, false]);
                    }
                    catch { continue; }
                    if (child is not null)
                    {
                        children.Add(WalkNode(child, maxDepth - 1));
                    }
                }
            }
        }
        return new SceneNodeInfo(name, nodeType.Name, path, children);
    }

    private static string? TryGetString(object target, Type type, string member)
    {
        try
        {
            var prop = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null)
            {
                return prop.GetValue(target)?.ToString();
            }
            var method = type.GetMethod(member, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            return method?.Invoke(target, null)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
