# Exception Enrichment Walkthrough

This walkthrough demonstrates Wick's core superpower: turning raw Godot C# exceptions into AI-actionable enriched diagnostics.

## 🎬 Watch the Demo

Record the demo locally (requires [asciinema](https://asciinema.org/)):

```bash
cd /path/to/Wick
asciinema rec -c "./docs/demo/record.sh" \
  --title "Wick — Roslyn-Enriched Exception Telemetry for Godot" \
  docs/demo/wick-demo.cast
```

Or play an existing recording: `asciinema play docs/demo/wick-demo.cast`

## The Scenario

You have a Godot C# game. Your `PlayerController` crashes with a `NullReferenceException` during gameplay. You're using an AI assistant (Claude, Cursor, Copilot, etc.) to help debug.

## Without Wick

Your AI assistant sees the raw stderr from Godot:

```
USER ERROR: System.NullReferenceException: Object reference not set to an instance of an object.
   at PlayerController.TakeDamage(Int32 amount) in /home/dev/game/scripts/PlayerController.cs:line 47
   at EnemyAI.Attack(PlayerController target) in /home/dev/game/scripts/EnemyAI.cs:line 89
   at EnemyAI._PhysicsProcess(Double delta) in /home/dev/game/scripts/EnemyAI.cs:line 23
```

The AI assistant now needs to:
1. Ask you to open `PlayerController.cs`
2. Read the file
3. Find line 47
4. Understand the method
5. Ask to see `EnemyAI.cs` for context
6. Read that file
7. Ask about the node tree setup
8. Ask to see recent logs
9. Finally form a hypothesis

That's **8+ turns** before the AI can even start suggesting a fix.

## With Wick

The AI calls `runtime_diagnose` and gets everything in one response:

```json
{
  "status": {
    "gameRunning": true,
    "pid": 4521,
    "exceptionCount": 1,
    "logEntries": 47
  },
  "exceptions": [
    {
      "raw": {
        "type": "System.NullReferenceException",
        "message": "Object reference not set to an instance of an object.",
        "frames": [
          {
            "type": "PlayerController",
            "method": "TakeDamage",
            "file": "PlayerController.cs",
            "line": 47,
            "isUserCode": true
          },
          {
            "type": "EnemyAI",
            "method": "Attack",
            "file": "EnemyAI.cs",
            "line": 89,
            "isUserCode": true
          }
        ]
      },
      "source": {
        "enclosingType": "PlayerController : CharacterBody3D",
        "methodBody": [
          "public void TakeDamage(int amount)",
          "{",
          "    _health -= amount;",
          "    _healthBar.Value = _health;  // ← LINE 47",
          "    if (_health <= 0)",
          "        Die();",
          "}"
        ],
        "nearestComment": "/// Applies damage to the player and updates the health bar UI.",
        "callers": [
          "EnemyAI.Attack(PlayerController target) at EnemyAI.cs:89",
          "TrapNode.OnBodyEntered(Node3D body) at TrapNode.cs:15",
          "PowerUp.ApplyEffect(PlayerController player) at PowerUp.cs:42"
        ]
      },
      "scene": {
        "scenePath": "/root/Level1",
        "nodeCount": 847
      }
    }
  ],
  "recentLogs": [
    "[4521.102] EnemyAI: Acquired target 'Player' at distance 3.2",
    "[4521.134] EnemyAI: Attack cooldown expired, attacking",
    "[4521.150] PlayerController: _Ready called",
    "[4521.150] PlayerController: WARNING: _healthBar node path not found"
  ]
}
```

The AI assistant immediately sees:
- **The bug:** `_healthBar` is null at line 47
- **The root cause:** The last log entry shows `_healthBar node path not found` — the node isn't wired up in the scene tree
- **The fix:** Check the `@export` path for the HealthBar node, or add a null check

**One turn. Zero file reads. Zero follow-up questions.**

## How to Reproduce This

### 1. Configure Wick

```json
{
  "mcpServers": {
    "wick": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/Wick/src/Wick.Server"],
      "env": {
        "WICK_GROUPS": "core,runtime,csharp,build",
        "WICK_GODOT_BIN": "/path/to/godot",
        "WICK_PROJECT_PATH": "/path/to/your-godot-project"
      }
    }
  }
}
```

### 2. Open Godot with the Wick plugin enabled

The plugin starts the Editor bridge on port 6505.

### 3. Ask your AI assistant

> "Launch the game and watch for crashes"

The AI calls `runtime_launch` which starts the game with exception capture.

### 4. Trigger the crash

Play the game until the bug occurs. Wick captures the exception in real-time.

### 5. Ask the AI to diagnose

> "What went wrong?"

The AI calls `runtime_diagnose` and gets the full enriched output shown above.

## The Difference

| Metric | Without Wick | With Wick |
|---|---|---|
| Turns to diagnosis | 8+ | 1 |
| Files AI needs to read | 2-3 | 0 |
| Context quality | Stack trace only | Method body + callers + logs + scene state |
| Time to fix | Minutes | Seconds |

## What Makes This Possible

1. **Roslyn Workspace** — Wick loads your project's Roslyn compilation and can query any symbol, method body, or reference instantly
2. **Multi-Source Capture** — Stderr (Tier 1) and in-process hooks (Tier 2) catch different classes of exceptions
3. **Pipeline Architecture** — Sources → Parser → Enricher → Buffer, running as a background service
4. **Bridge Integration** — Live scene tree queries via the Godot editor plugin provide game-state context

See [Exception Pipeline](../exception-pipeline.md) for the full technical deep-dive.
