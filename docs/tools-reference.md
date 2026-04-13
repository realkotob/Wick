# Tools Reference

Complete catalog of every MCP tool exposed by Wick, organized by pillar.

---

## Always Available

These tools are registered regardless of pillar configuration.

| Tool | Description |
|---|---|
| `wick_status` | Returns active tool groups and server version |
| `wick_list_groups` | Lists all available tool groups with activation status |
| `wick_tool_catalog` | Searches the full tool catalog by keyword |

---

## Core Pillar (`core`) — Always Active

Project discovery, scene file parsing, GDScript analysis.

### Godot Project Tools

| Tool | Description |
|---|---|
| `godot_status` | Returns Godot Engine provider status and capabilities |
| `godot_discover_projects` | Discovers Godot projects under a given root directory |
| `godot_project_info` | Reads detailed info from a `project.godot` file |
| `godot_read_scene` | Parses a `.tscn` scene file and returns the node tree structure |
| `godot_list_scenes` | Lists all scenes (`.tscn` files) in a project directory |
| `godot_list_scripts` | Lists all scripts (`.gd` and `.cs`) with language detection |

### GDScript LSP Tools

| Tool | Description |
|---|---|
| `gd_lsp_connect` | Connects to the GDScript Language Server |
| `gd_lsp_symbols` | Gets document symbols for a GDScript file |
| `gd_lsp_hover` | Gets hover documentation for a symbol at a position |
| `gd_lsp_definition` | Finds the definition of a symbol at a position |

---

## Runtime Pillar (`runtime`) — Opt-in

Game launching, exception capture, log streaming, live editor integration.

### Game Lifecycle

| Tool | Description |
|---|---|
| `runtime_launch` | Launches a Godot game process with exception capture |
| `runtime_stop` | Stops the currently running game process |
| `runtime_status` | Returns game process status, exception count, log count |

### Exception & Log Access

| Tool | Description |
|---|---|
| `runtime_get_exceptions` | Returns captured exceptions with Roslyn-enriched source context |
| `runtime_get_log_tail` | Returns the most recent log entries from the running game |
| `runtime_diagnose` | **Fan-out aggregator** — combines status + exceptions + logs in one call |

### Editor Bridge

| Tool | Description |
|---|---|
| `editor_bridge_status` | Returns connection status of Editor (6505) and Runtime (7777) bridges |
| `editor_bridge_connect` | Forces an immediate connection attempt to Editor or Runtime |
| `editor_scene_tree` | Gets the live scene tree as JSON from the running editor |
| `editor_node_properties` | Gets all properties of a specific node |
| `editor_call_method` | Invokes a method on a live node |
| `editor_set_property` | Sets a property value on a live node |
| `editor_run_scene` | Commands the editor to launch a scene |
| `editor_stop` | Commands the editor to stop the running scene |
| `editor_performance` | Gets performance metrics (FPS, draw calls, memory) |

### In-Process Runtime Query (Wick.Runtime companion)

| Tool | Description |
|---|---|
| `runtime_query_exceptions` | Queries exceptions from the in-process Wick.Runtime bridge |
| `runtime_query_logs` | Queries structured logs from the in-process bridge |
| `runtime_query_scene` | Queries scene state from the in-process bridge |

### DAP (Debug Adapter Protocol)

| Tool | Description |
|---|---|
| `gd_dap_launch` | Connects to Godot DAP (port 6006) and launches in debug mode |
| `gd_dap_stack_trace` | Gets the stack trace of a paused thread |
| `gd_dap_evaluate` | Evaluates a GDScript expression in a stack frame |
| `gd_dap_variables` | Gets variables in a specific scope/frame |

---

## C# Pillar (`csharp`) — Opt-in

Roslyn-powered code analysis and C# language server integration.

### Roslyn Analysis

| Tool | Description |
|---|---|
| `csharp_find_symbol` | Searches the workspace for declarations by name and kind |
| `csharp_find_references` | Finds all references to a symbol across the workspace |
| `csharp_member_signatures` | Returns member signatures for a type (methods, properties, fields) |

### C# LSP (csharp-ls)

| Tool | Description |
|---|---|
| `cs_lsp_connect` | Connects to the csharp-ls language server for a solution |
| `cs_lsp_symbols` | Gets document symbols for a C# file |
| `cs_lsp_hover` | Gets hover info for a symbol at a position |
| `cs_lsp_definition` | Finds the definition of a symbol at a position |

---

## Build Pillar (`build`) — Opt-in

.NET build, test, and package management with Roslyn enrichment.

| Tool | Description |
|---|---|
| `build_diagnose` | **Fan-out aggregator** — builds the project and returns Roslyn-enriched diagnostics |
| `dotnet_build` | Runs `dotnet build` with structured output |
| `dotnet_test` | Runs `dotnet test` with structured results |
| `dotnet_clean` | Runs `dotnet clean` |
| `nuget_add` | Adds a NuGet package |
| `nuget_remove` | Removes a NuGet package |
| `nuget_list` | Lists installed NuGet packages |

---

## Scene Pillar (`scene`) — Opt-in

Scene graph CRUD via headless Godot `--script` dispatch.

| Tool | Description |
|---|---|
| `scene_create` | Creates a new `.tscn` scene with a root node |
| `scene_add_node` | Adds a child node to an existing scene |
| `scene_save` | Saves modifications to a scene file |
| `scene_get_tree` | Returns the full node tree of a scene |
| `scene_get_node_properties` | Gets properties of a specific node in a scene |
| `scene_set_node_properties` | Sets properties on a specific node |
| `scene_load_resource` | Loads and inspects a Godot resource file |

---

## Activating Pillars

```bash
# Via environment variable
WICK_GROUPS=core,runtime,csharp,build dotnet run --project src/Wick.Server

# Via CLI flag
dotnet run --project src/Wick.Server -- --groups=all

# In MCP config
"env": { "WICK_GROUPS": "core,runtime,csharp,build" }
```
