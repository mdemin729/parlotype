---
title: Dependency Graph
type: architecture
status: active
tags: [architecture, dependencies]
last_updated: 2026-03-28
summary: Project dependency directions and external package usage
---

# Dependency Graph

## Project Dependencies

```
┌──────────────────┐     ┌───────────────────┐
│ Parlotype.Desktop │────▶│ Parlotype.Platform │
│   (Avalonia UI)   │     │ (Implementations)  │
└──────────────────┘     └────────┬───────────┘
                                  │
┌────────────────────┐            │
│ Parlotype.Benchmark│────────────┤
│   (Console CLI)    │            │
└────────────────────┘            ▼
                         ┌────────────────┐
                         │ Parlotype.Core │
                         │  (Contracts)   │
                         └────────────────┘

Test Projects:
  Tests ──────────────▶ Core, Platform
  Desktop.Tests ──────▶ Desktop, Core
  Benchmark.Tests ────▶ Benchmark, Core
```

## External Dependencies by Project

### Core (zero external dependencies)
- Pure domain interfaces and models

### Platform
- **Whisper.net** + **Whisper.net.Runtime** — speech recognition
- **Whisper.net.Runtime.Cuda** — GPU acceleration (optional, ~350 MB)
- **NAudio** — WASAPI audio capture
- **Microsoft.ML.OnnxRuntime** — Silero VAD inference
- **SharpHook** — global keyboard hooks

### Desktop
- **Avalonia** (11.3.0) — UI framework
- **CommunityToolkit.Mvvm** — MVVM source generators
- **Microsoft.Extensions.DependencyInjection** — DI container

### Benchmark
- **System.CommandLine** — CLI framework
- **Spectre.Console** — rich terminal output
- **Microsoft.Data.Sqlite** — historical run storage
