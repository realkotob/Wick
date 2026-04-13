#!/usr/bin/env bash
# ============================================================================
# Wick Demo Recording Script
#
# Usage:
#   asciinema rec -c "./docs/demo/record.sh" --title "Wick — Roslyn-Enriched Exception Telemetry for Godot" docs/demo/wick-demo.cast
#
# This script produces a polished, human-paced terminal demo of Wick's
# capabilities. It uses simulated typed commands and pre-baked output
# to showcase the MCP tool responses an AI assistant would see.
# ============================================================================

set -e

# --- Configuration ---
TYPING_SPEED=0.04    # seconds per character
PAUSE_SHORT=1.0      # short pause between steps
PAUSE_MEDIUM=2.0     # medium pause (reading time)
PAUSE_LONG=3.0       # long pause (section transitions)
PROMPT="\033[1;32m❯\033[0m "  # green arrow prompt

# --- Helpers ---
type_cmd() {
    local cmd="$1"
    printf "%b" "$PROMPT"
    for ((i=0; i<${#cmd}; i++)); do
        printf "%s" "${cmd:$i:1}"
        sleep "$TYPING_SPEED"
    done
    sleep 0.3
    printf "\n"
}

run_cmd() {
    type_cmd "$1"
    eval "$1"
    sleep "${2:-$PAUSE_SHORT}"
}

show_output() {
    # Print pre-baked output with realistic formatting
    echo "$1"
    sleep "${2:-$PAUSE_MEDIUM}"
}

section() {
    echo ""
    echo -e "\033[1;36m━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\033[0m"
    echo -e "\033[1;36m  $1\033[0m"
    echo -e "\033[1;36m━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\033[0m"
    echo ""
    sleep "$PAUSE_LONG"
}

comment() {
    echo -e "\033[0;90m# $1\033[0m"
    sleep "$PAUSE_SHORT"
}

# ============================================================================
#  ACT 1 — BUILD & VERIFY
# ============================================================================

clear
echo ""
echo -e "\033[1;35m    ╦ ╦╦╔═╗╦╔═\033[0m"
echo -e "\033[1;35m    ║║║║║  ╠╩╗\033[0m"
echo -e "\033[1;35m    ╚╩╝╩╚═╝╩ ╩\033[0m"
echo ""
echo -e "\033[0;37m    Roslyn-enriched C# exception telemetry for Godot Engine\033[0m"
echo -e "\033[0;37m    Exposed over the Model Context Protocol (MCP)\033[0m"
echo ""
sleep "$PAUSE_LONG"

section "ACT 1 — BUILD & VERIFY"

comment "Build with TreatWarningsAsErrors — 0 warnings is enforced"
run_cmd "dotnet build Wick.slnx --configuration Release 2>&1 | tail -3" "$PAUSE_MEDIUM"

comment "215 tests — 203 unit + 12 integration"
run_cmd "dotnet test Wick.slnx --configuration Release --no-build 2>&1 | grep -E 'Passed|Failed'" "$PAUSE_MEDIUM"

# ============================================================================
#  ACT 2 — THE HERO: EXCEPTION ENRICHMENT
# ============================================================================

section "ACT 2 — THE HERO: C# Exception Enrichment"

comment "Without Wick, your AI assistant sees this from Godot's stderr:"
sleep "$PAUSE_SHORT"

echo -e "\033[0;31m"
cat << 'RAW_EXCEPTION'
  USER ERROR: System.NullReferenceException:
    Object reference not set to an instance of an object.
     at PlayerController.TakeDamage(Int32 amount)
       in /home/dev/game/scripts/PlayerController.cs:line 47
     at EnemyAI.Attack(PlayerController target)
       in /home/dev/game/scripts/EnemyAI.cs:line 89
     at EnemyAI._PhysicsProcess(Double delta)
       in /home/dev/game/scripts/EnemyAI.cs:line 23
RAW_EXCEPTION
echo -e "\033[0m"

sleep "$PAUSE_MEDIUM"
comment "The AI needs 8+ turns to ask for files, read code, understand context..."
sleep "$PAUSE_LONG"

comment "With Wick, the AI calls runtime_diagnose and gets THIS:"
sleep "$PAUSE_SHORT"

echo -e "\033[0;33m"
cat << 'ENRICHED'
  {
    "exception": {
      "type": "System.NullReferenceException",
      "message": "Object reference not set to an instance of an object."
    },
    "source": {
      "enclosingType": "PlayerController : CharacterBody3D",
      "methodBody": [
        "public void TakeDamage(int amount)",
        "{",
        "    _health -= amount;",
ENRICHED
echo -e "\033[1;33m        \"    _healthBar.Value = _health;  // ← LINE 47 — _healthBar is null!\",\033[0m"
echo -e "\033[0;33m"
cat << 'ENRICHED2'
        "    if (_health <= 0) Die();",
        "}"
      ],
      "callers": [
        "EnemyAI.Attack(PlayerController) at EnemyAI.cs:89",
        "TrapNode.OnBodyEntered(Node3D) at TrapNode.cs:15"
      ],
      "nearestComment": "/// Applies damage and updates the health bar UI."
    },
ENRICHED2
echo -e "\033[0m"

echo -e "\033[0;32m"
cat << 'LOGS'
    "recentLogs": [
      "[4521.102] EnemyAI: Acquired target 'Player' at distance 3.2",
      "[4521.134] EnemyAI: Attack cooldown expired, attacking",
      "[4521.150] PlayerController: WARNING: _healthBar node path not found"
    ],
LOGS
echo -e "\033[0m"

echo -e "\033[0;36m"
cat << 'SCENE'
    "scene": { "scenePath": "/root/Level1", "nodeCount": 847 }
  }
SCENE
echo -e "\033[0m"

sleep "$PAUSE_LONG"

echo -e "\033[1;37m  The AI sees the bug immediately:\033[0m"
echo -e "\033[1;32m  ✓ _healthBar is null at line 47\033[0m"
echo -e "\033[1;32m  ✓ The log says '_healthBar node path not found'\033[0m"
echo -e "\033[1;32m  ✓ Fix: check the @export NodePath or add null guard\033[0m"
echo ""
echo -e "\033[1;35m  One turn. Zero file reads. Zero follow-up questions.\033[0m"
sleep "$PAUSE_LONG"

# ============================================================================
#  ACT 3 — C# INTELLIGENCE
# ============================================================================

section "ACT 3 — C# Intelligence (Roslyn-Powered)"

comment "Find all declarations of 'PlayerController' across the workspace"
type_cmd "# AI calls: csharp_find_symbol(name: 'PlayerController', kind: 'Type')"
sleep "$PAUSE_SHORT"

show_output '  {
    "symbols": [
      {
        "name": "PlayerController",
        "kind": "Class",
        "file": "scripts/PlayerController.cs",
        "line": 8,
        "signature": "public partial class PlayerController : CharacterBody3D"
      }
    ]
  }'

comment "Find every call site for TakeDamage across the entire project"
type_cmd "# AI calls: csharp_find_references(symbolName: 'TakeDamage')"
sleep "$PAUSE_SHORT"

show_output '  {
    "symbol": "PlayerController.TakeDamage(int)",
    "references": [
      { "file": "EnemyAI.cs",   "line": 89, "context": "target.TakeDamage(_attackDamage);" },
      { "file": "TrapNode.cs",  "line": 15, "context": "player.TakeDamage(trapDamage);" },
      { "file": "PowerUp.cs",   "line": 42, "context": "player.TakeDamage(-healAmount);" }
    ],
    "totalReferences": 3
  }'

comment "Build with enriched diagnostics — typos get fuzzy-matched"
type_cmd "# AI calls: build_diagnose()"
sleep "$PAUSE_SHORT"

show_output '  {
    "buildResult": "Failed",
    "diagnostics": [
      {
        "id": "CS0103",
        "message": "The name '\''helath'\'' does not exist in the current context",
        "file": "PlayerController.cs",
        "line": 47,
        "enrichment": {
          "signatureHint": "Did you mean: health, Health, _health?",
          "methodBody": "public void TakeDamage(int amount) { _helath -= amount; ... }"
        }
      }
    ]
  }'

# ============================================================================
#  ACT 4 — GODOT INTEGRATION
# ============================================================================

section "ACT 4 — Godot Integration"

comment "Discover all Godot projects under a root directory"
type_cmd "# AI calls: godot_discover_projects(rootPath: '/home/dev')"
sleep "$PAUSE_SHORT"

show_output '  {
    "projects": [
      {
        "name": "UsefulIdiots",
        "path": "/home/dev/UsefulIdiots",
        "hasCSharp": true,
        "scenes": 24,
        "gdScripts": 8,
        "csScripts": 147
      }
    ]
  }'

comment "Parse a scene file to see the node tree"
type_cmd "# AI calls: godot_read_scene(filePath: 'game/scenes/level_1.tscn')"
sleep "$PAUSE_SHORT"

show_output '  {
    "rootNode": {
      "name": "Level1",
      "type": "Node3D",
      "children": [
        { "name": "Player", "type": "CharacterBody3D", "script": "PlayerController.cs" },
        { "name": "Enemies", "type": "Node3D", "children": [
          { "name": "EnemyAI_01", "type": "CharacterBody3D", "script": "EnemyAI.cs" }
        ]},
        { "name": "Environment", "type": "Node3D", "children": [ "..." ] }
      ]
    },
    "totalNodes": 847
  }'

comment "Live editor bridge — query the running scene tree"
type_cmd "# AI calls: editor_scene_tree(target: 'editor')"
sleep "$PAUSE_SHORT"

show_output '  {
    "name": "Level1", "type": "Node3D", "path": "/root/Level1",
    "children": [
      { "name": "Player", "type": "CharacterBody3D", "path": "/root/Level1/Player" },
      { "name": "DirectionalLight3D", "type": "DirectionalLight3D" },
      { "name": "WorldEnvironment", "type": "WorldEnvironment" }
    ]
  }'

# ============================================================================
#  CLOSING
# ============================================================================

section "TOOL PILLARS"

echo -e "  \033[1;37mPillar    Tools  Description\033[0m"
echo -e "  \033[0;32mcore\033[0m      6      Project discovery, scene parsing, GDScript LSP"
echo -e "  \033[0;33mruntime\033[0m   15     Game launch, exception stream, editor bridge, DAP"
echo -e "  \033[0;34mcsharp\033[0m    7      Roslyn analysis, C# LSP, find symbol/references"
echo -e "  \033[0;35mbuild\033[0m     7      dotnet CLI, NuGet management, build diagnostics"
echo -e "  \033[0;36mscene\033[0m     7      Scene graph CRUD via headless Godot dispatch"
echo ""
echo -e "  \033[0;90mActivate:  WICK_GROUPS=core,runtime,csharp  or  --groups=all\033[0m"
sleep "$PAUSE_LONG"

echo ""
echo -e "\033[1;35m    ╦ ╦╦╔═╗╦╔═\033[0m"
echo -e "\033[1;35m    ║║║║║  ╠╩╗\033[0m"
echo -e "\033[1;35m    ╚╩╝╩╚═╝╩ ╩\033[0m"
echo ""
echo -e "\033[0;37m    github.com/buildepicshit/Wick\033[0m"
echo -e "\033[0;37m    MIT Licensed • .NET 10 • Godot 4.6.1+\033[0m"
echo ""
echo -e "\033[0;90m    Built by Build Epic Shit Studios ⚒️\033[0m"
echo ""
sleep "$PAUSE_LONG"
