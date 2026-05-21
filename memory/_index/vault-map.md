---
title: Vault Map
type: index
status: active
last_updated: 2026-05-21
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
│   ├── audio-pipeline.md     # WASAPI → VAD → Whisper / Gemma 4 → Injection
│   ├── dependency-graph.md   # Project dependencies and external boundaries
│   └── subsystems.md         # Text injection, hotkeys, settings, logging, LlamaServer
├── services/
│   ├── _index.md             # Service registry (8 projects)
│   ├── core.md               # Parlotype.Core profile
│   ├── platform.md           # Parlotype.Platform profile
│   ├── desktop.md            # Parlotype.Desktop profile
│   ├── benchmark.md          # Parlotype.Benchmark profile
│   ├── tests.md              # Parlotype.Tests profile
│   └── benchmark-tests.md    # Parlotype.Benchmark.Tests profile
├── conventions/
│   ├── _index.md             # Convention summary
│   ├── dotnet-standards.md   # .NET 10, nullable, warnings-as-errors, GPU runtime
│   ├── avalonia-patterns.md  # Avalonia 12, AXAML, MVVM, flyouts, threading
│   └── testing-strategy.md   # xUnit v2 + v3, Skia headless screenshots, benchmarks
├── decisions/
│   └── _index.md             # Links to ADRs in docs/decisions/ (29 ADRs)
├── sessions/                 # Episodic memory (handoff notes)
│   ├── _template.md          # Session handoff template
│   └── YYYY-MM-DD-HHMM-<slug>.md   # Per-session notes
├── knowledge/                # Semantic memory — learned facts
│   ├── _index.md
│   ├── agent-skills.md
│   ├── asyncrelaycommand-flicker.md
│   ├── avalonia-devtools.md
│   ├── benchmark-pipeline-recommendations.md
│   ├── gemma4-cuda-blackwell.md
│   ├── llama-cpp-release-assets.md
│   ├── llama-server-hf-download.md
│   ├── llamacpp-gemma4-integration.md
│   ├── sharphook-suppress-event.md
│   ├── vad-silence-threshold-constraint.md
│   ├── vulkan-runtime-probing.md
│   ├── whisper-net-quirks.md
│   └── whisper-translation-models.md
├── scripts/
│   ├── generate-index.sh     # Vault stats, missing-frontmatter & orphan detection
│   └── check-staleness.sh    # Flag stale documents (skips sessions/)
└── .gitignore                # Excludes Obsidian workspace/plugin data
```

> **Note on `skills/`** — agent skills no longer live under `memory/skills/`. They are auto-discovered from `.claude/skills/<kebab-name>/SKILL.md` (with `name` + `description` frontmatter). See [[agent-skills]].

## Memory Layers

| Layer | Location | Purpose | Volatility |
|-------|----------|---------|------------|
| **Procedural** | `conventions/`, `.claude/skills/` | How to do things | Low — changes with conventions |
| **Semantic** | `architecture/`, `services/`, `knowledge/` | Facts about the codebase | Medium — changes with code |
| **Episodic** | `sessions/` | What happened in past sessions | High — new entries each session |
| **Decisions** | `decisions/` | Why things are the way they are | Low — append-only |

## Staleness Convention

- Every note in `_index/`, `architecture/`, `services/`, `conventions/`, `decisions/`, and `knowledge/` carries a `last_updated: YYYY-MM-DD` field in its YAML frontmatter.
- `sessions/` notes are **episodic** and dated by filename; they intentionally do not carry `last_updated`, and `scripts/check-staleness.sh` skips that directory.
- The default staleness threshold is **90 days**.

