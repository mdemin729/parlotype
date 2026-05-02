---
title: Parlotype Memory Vault
type: router
status: active
last_updated: 2026-04-28
summary: Root router for AI agent orientation in the Parlotype voice-to-text project
---

# Parlotype — Agent Memory Vault

Parlotype is a local-first, privacy-focused voice-to-text desktop app. All speech recognition runs on-device via Whisper.net. Built with .NET 10 + Avalonia UI.

## Quick Commands

```bash
dotnet build Parlotype.slnx                    # Build (zero warnings required)
dotnet test                                     # All tests
dotnet test -p:EnableCuda=false                 # CPU-only tests
dotnet run --project src/Parlotype.Desktop.V2   # Launch app
```

## Navigation

| Need to know about... | Read this |
|----------------------|-----------|
| Full vault contents | [[vault-map]] |
| Domain terminology | [[glossary]] |
| Project architecture | [[architecture/_index]] |
| Audio pipeline flow | [[audio-pipeline]] |
| Individual projects | [[services/_index]] |
| Coding conventions | [[conventions/_index]] |
| Past design decisions | [[decisions/_index]] |
| Session handoffs | [[sessions/_template]] |
| Learned knowledge | [[knowledge/_index]] |
| Obsidian markdown guide | [[obsidian-markdown]] |
| Session start/end protocol | [[session-management]] |
| Debugging audio pipeline | [[debug-pipeline]] |
| Adding new features | [[implement-feature]] |

## Dependency Direction

```
Desktop.V2 → Platform → Core
Benchmark → Platform → Core
Tests → Core, Platform
Desktop.V2.Tests → Desktop.V2, Core
Benchmark.Tests → Benchmark, Core
```

## Key Architectural Constraints

- **Privacy-first**: voice data never leaves the device
- **Warnings as errors**: `TreatWarningsAsErrors=true` in `Directory.Build.props`
- **Interfaces in Core, implementations in Platform**: never add platform packages to Core
- **All services are singletons** registered in `PlatformServiceExtensions.cs`
- **Whisper model lifecycle**: never load multiple models simultaneously; sequential load→unload→load is supported via `UnloadAsync()` (ADR-017)

## Definition of Done

A non-trivial change isn't finished when the build is green — it's finished when the next agent can find it. Before declaring complete:

1. **Build/tests/behaviour verified** (zero warnings, tests pass, end-to-end exercised).
2. **ADR required** if the change adds a Core interface, a `PlatformServiceExtensions` registration, a new `.csproj` dependency, an OS/build-flag-conditional behaviour, a new native/P-Invoke call, or touches audio/hotkey/settings/Whisper subsystems.
3. **Vault updated** when public symbols/services/subsystems change: `memory/services/<project>.md`, `memory/decisions/_index.md`, and `memory/architecture/subsystems.md` as applicable.
4. **Knowledge captured** for non-derivable facts (third-party quirks, environment gotchas) under `memory/knowledge/`.
5. **Ask, don't prune silently** — if deferring (2)–(4), surface that to the user via `ask_user` before completing.

Full rules and triggers: see "Definition of Done" section in `CLAUDE.md`.
