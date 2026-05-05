---
title: Vault Map
type: index
status: active
last_updated: 2026-03-28
summary: Master index of the entire Parlotype memory vault
---

# Vault Map

## Structure

```
memory/
├── CLAUDE.md                 # Root router (always loaded first)
├── _index/
│   ├── vault-map.md          # This file — master index
│   └── glossary.md           # Domain terminology
├── architecture/
│   ├── _index.md             # Architecture overview
│   ├── audio-pipeline.md     # WASAPI → VAD → Whisper → Injection
│   ├── dependency-graph.md   # Project dependencies and data flow
│   └── subsystems.md         # Text injection, hotkeys, settings, logging
├── services/
│   ├── _index.md             # Service registry
│   ├── core.md               # Parlotype.Core profile
│   ├── platform.md           # Parlotype.Platform profile
│   ├── desktop.md            # Parlotype.Desktop profile
│   ├── benchmark.md          # Parlotype.Benchmark profile
│   ├── tests.md              # Parlotype.Tests profile
│   ├── desktop-tests.md      # Parlotype.Desktop.Tests profile
│   └── benchmark-tests.md    # Parlotype.Benchmark.Tests profile
├── conventions/
│   ├── _index.md             # Convention summary
│   ├── dotnet-standards.md   # .NET 10, nullable, warnings-as-errors
│   ├── avalonia-patterns.md  # AXAML, MVVM, flyouts, compiled bindings
│   └── testing-strategy.md   # xUnit, headless UI, benchmarks
├── decisions/
│   └── _index.md             # Links to ADRs in docs/decisions/
├── sessions/
│   └── _template.md          # Session handoff template
├── knowledge/
│   └── _index.md             # Semantic memory — learned facts
├── skills/
│   ├── obsidian-markdown.md  # Obsidian-flavored markdown skill
│   ├── session-management.md # Session start/end protocols
│   ├── debug-pipeline.md     # Debugging audio pipeline
│   └── implement-feature.md  # Adding new features
├── scripts/
│   ├── generate-index.sh     # Rebuild _index files from vault
│   └── check-staleness.sh    # Flag stale documents
└── .gitignore                # Excludes Obsidian workspace/plugin data
```

## Memory Layers

| Layer | Location | Purpose | Volatility |
|-------|----------|---------|------------|
| **Procedural** | `conventions/`, `skills/` | How to do things | Low — changes with conventions |
| **Semantic** | `architecture/`, `services/`, `knowledge/` | Facts about the codebase | Medium — changes with code |
| **Episodic** | `sessions/` | What happened in past sessions | High — new entries each session |
| **Decisions** | `decisions/` | Why things are the way they are | Low — append-only |
