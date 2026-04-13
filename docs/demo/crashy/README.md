# Demo: Crash Fixture

Minimal C# scripts that deliberately crash with a `NullReferenceException`. Used to demonstrate Wick's exception enrichment — the before/after shown in the project README.

**These are NOT part of Wick itself.** They're standalone Godot C# scripts you drop into any Godot 4.6.1+ mono project to reproduce the demo crash.

## The Bug

`PlayerController.TakeDamage()` accesses `_healthBar.Value`, but `_healthBar` is null because the `HealthBarPath` export wasn't set in the scene editor. When `EnemyAI.Attack()` calls `TakeDamage()`, it crashes.

## Without Wick

Your AI sees a raw stack trace and spends 8+ turns asking to open files.

## With Wick

Your AI calls `runtime_diagnose` and gets the method body, caller chain, logs, and scene context in one response. Fix in one turn.

## Usage

1. Copy `PlayerController.cs` and `EnemyAI.cs` into a Godot 4.6.1+ mono project
2. Create a scene with a `PlayerController` node (add it to the "player" group) and an `EnemyAI` node nearby
3. **Don't** set the `HealthBarPath` export on the PlayerController
4. Run the scene — the crash happens within seconds when the enemy attacks
