---
project: Wick
phase: v0.1.0-oss-launch
last_shipped: 2026-04-13
last_updated: 2026-04-13T12:00-07:00
tests:
  total: 219
  passing: 219
  failing: 0
blockers: []
next_milestone: Real-world validation and community feedback
version: 0.1.0
dotnet: 10.0.201
target_framework: net10.0
repo_visibility: public
---

# Wick — Project Status

> **Snapshot as of 2026-04-13.** This file answers *"where is Wick right now?"* — current phase, recent work, test and build state, next steps. Engineering standards and architecture: [`AGENTS.md`](AGENTS.md). Project overview: [`README.md`](README.md).

## What Wick Is

Wick is a native .NET 10 Model Context Protocol server that captures unhandled C# exceptions from a running Godot game, enriches each exception with Roslyn-powered source context (calling method body, surrounding lines, caller chain), and exposes the enriched exception stream to AI coding assistants over MCP. The value proposition is narrow and deliberate: **when a Godot C# game crashes, the agent sees the exception with full source-level context and can fix it in one turn instead of ten.**

**Version:** `0.1.0` (first public release)
**.NET:** `10.0.201` SDK, targeting `net10.0`, C# 14
**Target Godot:** `4.6.1-stable-mono`
**License:** MIT

## Current Phase: v0.1.0 — First Public Release

All planned features are implemented and tested. The project completed six sub-specs covering the full tool surface:

| Sub-spec | What Shipped |
|---|---|
| **A** — Runtime exception pipeline | `GodotExceptionParser`, `ExceptionEnricher`, `ExceptionPipeline` (hosted service), `BridgeExceptionSource`, `ProcessExceptionSource`, `runtime_diagnose` fan-out |
| **B** — Static tool group system | 5-pillar model (`core`/`runtime`/`csharp`/`scene`/`build`), `ToolGroupResolver`, 5 runtime MCP tools |
| **C** — Scene pillar | 2 read-only tools (`.tscn` parsing) + 5 mutation tools (headless `godot --script` dispatch) |
| **D** — C# analysis tools | `csharp_find_symbol`, `csharp_find_references`, `csharp_member_signatures` via Roslyn workspace |
| **E** — Build intelligence | 7 build tools with Roslyn-enriched diagnostics |
| **F** — Wick.Runtime NuGet companion | In-process exception hooks (`TaskScheduler` + `AppDomain`), TCP bridge, `WickRuntime.Install()` |

## Test & Build State

| Metric | Value |
|---|---|
| Unit tests | 207 (`Wick.Tests.Unit`) |
| Integration tests | 12 (`Wick.Tests.Integration`) |
| **Total** | **219** |
| Passing | 219 (100%) |
| Failing | 0 |
| Skipped | 0 |
| Build warnings | 0 (`TreatWarningsAsErrors=true` repo-wide) |
| Framework | `net10.0` |

Canonical verification: `dotnet build Wick.slnx --configuration Release && dotnet test Wick.slnx --configuration Release`.

## Next Up

1. **Real-world validation** — Wire Wick into active Godot C# projects, fix rough edges under real debugging pressure.
2. **Community feedback** — Respond to issues, discussions, and feature requests from early adopters.

## Blockers

None.

## References

- [`AGENTS.md`](AGENTS.md) — cross-framework operating manual
- [`README.md`](README.md) — project overview and positioning
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — human contributor onboarding
- [`CHANGELOG.md`](CHANGELOG.md) — version-by-version change log
- [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) — community standards
- [`SECURITY.md`](SECURITY.md) — vulnerability disclosure
- [`ATTRIBUTION.md`](ATTRIBUTION.md) — license acknowledgements
