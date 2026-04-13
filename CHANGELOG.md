# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] — 2026-04-13

First public release. Wick is a native .NET 10 MCP server for Godot Engine, providing Roslyn-enriched C# exception telemetry and a comprehensive tool surface across five pillars.

### Added

- **Runtime exception pipeline** — Multi-source exception capture (`ProcessExceptionSource` for stderr, `BridgeExceptionSource` for TCP bridge) with `GodotExceptionParser`, `ExceptionEnricher` (Roslyn source context: method body, callers, signature hints), `ExceptionPipeline` hosted service, and thread-safe ring buffers
- **Tool group system** — 5-pillar static tool activation (`core`, `runtime`, `csharp`, `build`, `scene`) via `WICK_GROUPS` env var or `--groups` CLI flag, with `ToolGroupResolver` and introspection tools (`wick_status`, `wick_list_groups`, `wick_tool_catalog`)
- **Scene pillar** — 7 scene tools: 2 read-only (`.tscn` parsing with hierarchical tree and per-node properties) + 5 mutation tools via headless `godot --script` dispatch
- **C# analysis pillar** — `csharp_find_symbol`, `csharp_find_references`, `csharp_member_signatures` via Roslyn workspace
- **Build intelligence pillar** — 7 build tools with `BuildDiagnosticParser` + `BuildDiagnosticEnricher` for Roslyn-enriched diagnostics, plus `build_diagnose` fan-out aggregator
- **Wick.Runtime companion** — In-process NuGet package hooking `TaskScheduler.UnobservedTaskException`, structured logging provider, TCP bridge server for runtime introspection
- **Runtime pillar** — Game lifecycle management (`runtime_launch`, `runtime_stop`, `runtime_status`), exception stream (`runtime_get_exceptions`), log tail (`runtime_get_log_tail`), and `runtime_diagnose` fan-out aggregator
- **Editor bridge tools** — Live scene tree, node properties, method invocation, property setting, performance metrics via TCP JSON-RPC bridge to Godot editor plugin (port 6505)
- **GDScript support** — Script parsing (functions, signals, exports, classes), LSP integration (symbols, hover, definition via port 6005), DAP debugging tools, script template generation
- **C# LSP integration** — `csharp-ls` integration for symbols, hover, definition
- **Godot project tools** — Project discovery, project info, scene listing, script listing with language detection
- **GDScript editor plugin** — `addons/wick/` with TCP JSON-RPC server (port 6505 editor, port 7777 runtime) and `scene_ops.gd` for headless scene dispatch
- **Public documentation** — Getting started guide, architecture overview, exception pipeline deep-dive, tools reference, exception enrichment walkthrough with before/after comparison
- **Community health files** — MIT license, ATTRIBUTION.md (GoPeak credits), CONTRIBUTING.md, CODE_OF_CONDUCT.md (Contributor Covenant 2.1), SECURITY.md, AGENTS.md (cross-framework operating manual)
- **Build infrastructure** — .NET 10 / C# 14 single-target `net10.0`, `TreatWarningsAsErrors=true` repo-wide, central package management via `Directory.Packages.props`, `.editorconfig` with C# style rules, `.gitattributes` for LF normalization, GitHub Actions CI with test artifact upload
- **Test suite** — 219 tests (207 unit + 12 integration) using xUnit v3, FluentAssertions, NSubstitute
- **Security hardening** — Input validation on build tool parameters (configuration, package names, versions), generic error messages for scene dispatch failures, GDScript identifier validation for code generation

### Attribution

Wick is a clean-room C# reimplementation inspired by [GoPeak](https://github.com/HaD0Yun/Gopeak-godot-mcp) (MIT, © 2025 Solomon Elias / HaD0Yun). See [ATTRIBUTION.md](ATTRIBUTION.md) for detailed credits.
