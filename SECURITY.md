# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Wick, please report it responsibly.

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please use [GitHub's private vulnerability reporting](https://github.com/buildepicshit/Wick/security/advisories/new). We will acknowledge receipt within 48 hours and provide a timeline for a fix.

## Scope

Wick is a developer tool (MCP server) that runs locally on developer machines. It communicates via:

- **stdio** — with the MCP client (e.g., IDE extensions)
- **TCP JSON-RPC** — with the Godot editor plugin (localhost port 6505)
- **TCP JSON-RPC** — with the Godot runtime addon (localhost port 7777)

Wick does not expose any network services to external hosts by default.

## Supported Versions

| Version | Supported |
|---|---|
| Latest release | ✅ |
| Previous minor | Best effort |
| Older | ❌ |
