---
title: Dependency Graph
type: architecture
status: active
tags: [architecture, dependencies]
last_updated: 2026-05-21
summary: Project dependency directions and external package usage (8 projects, Avalonia 12, Gemma 4 + llama.cpp engines)
---

# Dependency Graph

## Project Dependencies

```
┌──────────────────┐     ┌────────────────────┐
│ Parlotype.Desktop │────▶│ Parlotype.Platform │
│  (Avalonia 12 UI) │     │ (Implementations)  │
└──────────────────┘     └────────┬───────────┘
                                  │
┌────────────────────┐            │
│ Parlotype.Benchmark│────────────┤──────────▶ Parlotype.Gemma4
│   (Console CLI)    │            │            (Python sidecar
└────────────────────┘            ▼             ASR — benchmark)
                         ┌────────────────┐
                         │ Parlotype.Core │
                         │  (Contracts)   │
                         └────────────────┘

Test Projects:
  Parlotype.Tests ─────────▶ Core, Platform
  Parlotype.Desktop.Tests ─▶ Core, Desktop
  Parlotype.Benchmark.Tests ▶ Core, Benchmark, Gemma4
```

The 8 projects are wired in `Parlotype.slnx`. `Parlotype.Desktop` is the sole desktop frontend (V1 sunset in [[decisions/_index|ADR-018]]; was previously called `Parlotype.Desktop.V2`).

## External Dependencies by Project

### Parlotype.Core (zero external dependencies)
- Pure domain interfaces and models. Includes `LlamaServer` contracts (`ILlamaServerCatalog`, `ILlamaServerInstaller`, `ILlamaServerRegistry`) since [[decisions/_index|ADR-026]].

### Parlotype.Platform
- **Whisper.net** + **Whisper.net.Runtime** — speech recognition
- **Whisper.net.Runtime.Vulkan** — the only GPU runtime, cross-vendor (always included, ~30 MB) — [[decisions/_index|ADR-022]], [[decisions/_index|ADR-049]]
- **NAudio** — WASAPI audio capture
- **Microsoft.ML.OnnxRuntime** — Silero VAD inference (arrives as `Microsoft.ML.OnnxRuntime.Gpu` via `SileroVad`; its CUDA/TensorRT provider natives are filtered out of build and publish output by `Directory.Build.targets` — [[decisions/_index|ADR-050]])
- **SharpHook 7.x** — global keyboard hooks (uses `SimpleGlobalHook` for working event suppression — [[decisions/_index|ADR-020]])

### Parlotype.Desktop
- **Avalonia 12.0.2** — UI framework (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`; its Skia rendering backend pulls in `SkiaSharp.NativeAssets.Win32`/`HarfBuzzSharp.NativeAssets.Win32`, whose native PDBs (`libSkiaSharp.pdb`, `libHarfBuzzSharp.pdb`, ~100 MB combined) are filtered out of Release publish output by `Directory.Build.targets` — [[decisions/_index|ADR-052]])
- **CommunityToolkit.Mvvm** — MVVM source generators
- **Microsoft.Extensions.DependencyInjection** — DI container
- **ZLogger** — structured logging
- **AvaloniaUI.DiagnosticsSupport** (Debug-only) — Avalonia 12 devtools replacement ([[decisions/_index|ADR-016]])

### Parlotype.Benchmark
- **System.CommandLine** — CLI framework
- **Spectre.Console** — rich terminal output
- **Microsoft.Data.Sqlite** — historical run storage

### Parlotype.Gemma4
- Wraps a Python FastAPI sidecar (auto-managed) exposing Gemma 4 ASR ([[decisions/_index|ADR-024]]). Benchmark-only.

## External Process Boundaries

| Process | Purpose | Spawned by | ADR |
|---------|---------|-----------|-----|
| `llama-server.exe` (llama.cpp) | Gemma 4 inference for desktop + benchmark | `LlamaCppSpeechRecognizer` (Platform); managed install via `LlamaServer` subsystem | ADR-025, ADR-026, ADR-027 |
| Python FastAPI sidecar | Gemma 4 inference for benchmark (alternative path) | `Parlotype.Gemma4` | ADR-024 |

