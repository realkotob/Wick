namespace Wick.Core;

/// <summary>
/// Thin interface for querying Godot bridge state from the enricher.
/// Defined in Core to avoid circular deps. Will be implemented by
/// GodotBridgeManager in Wick.Providers.Godot (Part 3 wiring).
/// </summary>
public interface IGodotBridgeManagerAccessor
{
    bool IsEditorConnected { get; }
    SceneContext? GetSceneContext();
}
