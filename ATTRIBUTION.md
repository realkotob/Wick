# Attribution

Wick owes its existence to the pioneering work of the [GoPeak](https://github.com/HaD0Yun/Gopeak-godot-mcp) project, which established the foundational design for a Godot-focused MCP server.

## GoPeak — Godot MCP Server

- **License:** MIT
- **Original Author:** Solomon Elias
- **Enhancements:** HaD0Yun
- **Foundation:** Coding-Solo ([godot-mcp](https://github.com/Coding-Solo/godot-mcp))
- **Repository:** [github.com/HaD0Yun/Gopeak-godot-mcp](https://github.com/HaD0Yun/Gopeak-godot-mcp)

## What We Borrowed (Concepts, Not Code)

Wick is a **clean-room reimplementation** in C#. No source code was copied from GoPeak. The following *concepts and architectural ideas* were inspired by GoPeak's design:

| Concept | GoPeak Origin | Wick Implementation |
|---|---|---|
| **Tool Group System** | Dynamic tool groups that activate on-demand to reduce token usage | Reimplemented in C# with `ToolGroup` model and keyword-based auto-activation |
| **Tool Catalog** | `tool.catalog` search that auto-activates matching groups | Will be reimplemented as MCP tool with same UX pattern |
| **Compact/Full Profiles** | Environment variable toggle between ~35 core tools and full set | Same concept, C# implementation via DI configuration |
| **Editor Bridge** | WebSocket bridge to Godot editor plugin on port 6505 | Will be reimplemented as C# WebSocket client |
| **Runtime Addon** | TCP connection to in-game addon on port 7777 | Will be reimplemented as C# TCP client |
| **GDScript LSP/DAP Ports** | Port 6005 (LSP) and 6006 (DAP) conventions | Same port conventions, C# LSP/DAP protocol clients |
| **Scene CRUD Pattern** | Create/modify/delete nodes and resources via MCP tools | Reimplemented with C# `.tscn` text parser |

## What Wick Adds

These capabilities are entirely new and do not exist in GoPeak:

- **C#/.NET Provider** — Roslyn-based AST analysis for code navigation and understanding
- **`dotnet` CLI Integration** — Build, test, clean, package management
- **NuGet Management** — Add/update/remove packages, resolve conflicts
- **Roslyn-enriched exception telemetry** — stderr-captured C# exceptions enriched with calling method body, surrounding lines, caller chain
- **In-process exception capture** — Wick.Runtime NuGet companion wrapping TaskScheduler.UnobservedTaskException
- **C# analysis tools** — find symbol, find references, member signatures via Roslyn workspace
- **Build diagnostics enrichment** — dotnet build errors enriched with Roslyn source context

## Thank You

To Solomon Elias, HaD0Yun, and Coding-Solo: your work on GoPeak gave us the blueprint. Wick exists because you showed the community what a great Godot MCP server looks like. We're building on your shoulders.
