using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Abstraction over headless Godot subprocess dispatch for scene mutations.
/// Allows unit-testing SceneTools without spawning real Godot processes.
/// </summary>
public interface ISceneDispatchClient
{
    /// <summary>
    /// Invokes a scene operation via <c>godot --headless --script addons/wick/scene_ops.gd</c>.
    /// </summary>
    /// <param name="operation">Operation name (e.g. "create_scene", "add_node").</param>
    /// <param name="args">JSON-serializable arguments for the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed result from the GDScript dispatch script.</returns>
    Task<SceneModifyResult> DispatchAsync(
        string operation,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken = default);
}
