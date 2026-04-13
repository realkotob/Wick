# AGENTS.md — Wick

> Cross-framework operating manual for AI coding assistants and human contributors. For human-oriented setup, see [`CONTRIBUTING.md`](CONTRIBUTING.md). For current project state, see [`STATUS.md`](STATUS.md).

## What Wick Is

Wick is a native .NET 10 [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server for Godot Engine. It captures unhandled C# exceptions from a running Godot game, enriches each exception with Roslyn-powered source context (method body, surrounding lines, caller chain), and exposes the enriched exception stream to AI coding assistants over MCP. Clean-room reimplementation inspired by [GoPeak](https://github.com/HaD0Yun/Gopeak-godot-mcp) (MIT, © 2025 Solomon Elias).

**Architecture:** Wick runs as an **external process** — it does NOT run inside Godot. Communication happens over stdio (MCP protocol to AI clients) and TCP JSON-RPC (bridge to a GDScript editor plugin on ports 6505/7777). This lets Wick target `net10.0` even though Godot 4.6.1's runtime is stuck on `net8.0`.

## Build & Test

```bash
dotnet build Wick.slnx --configuration Release
dotnet test Wick.slnx --configuration Release
```

Both commands must produce **zero warnings and zero failures** — `TreatWarningsAsErrors=true` is enforced repo-wide.

### Requirements

- **.NET 10 SDK** — pinned to `10.0.201` via `global.json`
- **Single-target `net10.0`** — do not multi-target or downshift to `net8.0`
- **xUnit v3** with FluentAssertions and NSubstitute for tests
- **Central package management** — all NuGet versions live in `Directory.Packages.props`, not in individual `.csproj` files

## Architecture

### Tool Pillars

| Pillar | What It Does | Default |
|---|---|---|
| `core` | Project discovery, scene listing, GDScript parsing/LSP | Always on |
| `runtime` | Game launch, exception capture, log streaming, editor bridge, DAP | Opt-in |
| `csharp` | Roslyn analysis (find symbol, references, signatures), C# LSP | Opt-in |
| `build` | `dotnet build/test/clean` with Roslyn-enriched diagnostics, NuGet management | Opt-in |
| `scene` | Scene graph reads + mutation via headless Godot dispatch | Opt-in |

Activate pillars: `WICK_GROUPS=core,runtime,csharp` or `--groups=all`.

### Providers

| Project | Responsibility |
|---|---|
| `src/Wick.Providers.GDScript` | GDScript parsing, LSP client (port 6005), DAP |
| `src/Wick.Providers.CSharp` | Roslyn analysis, `csharp-ls` LSP, build tools |
| `src/Wick.Providers.Godot` | Scene parsing, editor/runtime bridge (ports 6505/7777), game launcher |
| `src/Wick.Core` | Shared types, exception pipeline, enrichment |
| `src/Wick.Server` | MCP server entry point, tool registration, DI |
| `src/Wick.Runtime` | In-process NuGet companion (exception hooks, TCP bridge) |

### Godot Bridge

The GDScript plugin at `addons/wick/` runs a TCP JSON-RPC server. RPC method names on the C# side **must match the GDScript dispatch table exactly** — editor-side methods are prefixed `editor_` (e.g. `editor_scene_tree`). Check `addons/wick/mcp_json_rpc_server.gd` for the dispatch table before adding new bridge calls — mismatched names fail silently.

## Project Structure

```
src/
├── Wick.Server/               # MCP server entry point
├── Wick.Core/                 # Shared types, exception pipeline
├── Wick.Runtime/              # In-process NuGet companion
├── Wick.Providers.GDScript/   # GDScript parsing, LSP, DAP
├── Wick.Providers.CSharp/     # Roslyn analysis, C# LSP, build tools
└── Wick.Providers.Godot/      # Scene parsing, Godot bridge

addons/wick/                   # GDScript editor plugin

tests/
├── Wick.Tests.Unit/           # Unit tests
└── Wick.Tests.Integration/    # Integration tests (real MCP server)

Directory.Build.props          # Repo-wide project defaults
Directory.Packages.props       # Central NuGet version management
global.json                    # .NET SDK pin
Wick.slnx                     # Solution file
```

## Code Conventions

- **Conventional Commits** — `<type>(<scope>): <description>` (e.g. `fix(godot): handle null scene tree`)
- **Squash merge only** — linear history on `main`
- **PR-only workflow** — no direct pushes to `main` (branch protection enforced)
- **TDD** — write the failing test first, then implement
- Catch specific exception types, not bare `catch { }`
- Don't suppress warnings with `#pragma warning disable` — fix the underlying issue
- Don't add `Version=` to `<PackageReference>` — use `Directory.Packages.props`
- Don't duplicate project settings from `Directory.Build.props` in individual `.csproj` files

## Reference Documentation

- [Model Context Protocol spec](https://spec.modelcontextprotocol.io/)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Godot Engine 4.x docs](https://docs.godotengine.org/en/stable/)
- [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc)
- [Roslyn API](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline)
- [FluentAssertions](https://fluentassertions.com/)
- [JSON-RPC 2.0 spec](https://www.jsonrpc.org/specification)
