---
title: Parlotype.Benchmark
type: service-profile
status: active
tags: [benchmark, cli, wer, cer, quality, gemma4, llamacpp]
criticality: medium
last_updated: 2026-05-21
summary: Console CLI for evaluating transcription quality with WER/CER/RTF metrics — supports Whisper and Gemma 4 (llama.cpp) engines
---

# Parlotype.Benchmark

## Purpose
Command-line tool for measuring and tracking transcription quality. Supports historical runs, comparisons, parameter sweeps, repetition stability analysis, and CI regression detection.

## Key Paths
- `src/Parlotype.Benchmark/Configuration/` — config models, sweep expansion (`SweepConfig`, `SweepExpander`)
- `src/Parlotype.Benchmark/Metrics/` — WER/CER/RTF calculators, text normalization
- `src/Parlotype.Benchmark/Pipeline/` — benchmark execution pipeline, `PipelineSimulator` (real-time flush behaviour)
- `src/Parlotype.Benchmark/Results/` — SQLite index (`benchmarks.db`), result models
- `src/Parlotype.Benchmark/Reporting/` — CSV / Markdown / JSON exporters

## CLI Commands
```bash
run       # Execute benchmark with config
list      # List historical runs
compare   # Compare two runs (delta metrics)
export    # Export run (csv, markdown, json)
import    # Rebuild SQLite index from JSON
sweep     # Cartesian product parameter sweep
check     # CI regression detection (exit 0 pass / 1 fail)
```

## Conventions
- Use Spectre.Console for output, never `Console.WriteLine`
- Results auto-indexed into `benchmarks.db` SQLite after each run
- Sweeps use `SweepConfig` with dot-notation axes (e.g., `whisper.model`, `whisper.beamSize`)
- Repetitions: `repetitions > 1` for mean/stddev stability analysis

## Dependencies
- [[platform]], [[core]]
- System.CommandLine, Spectre.Console, Microsoft.Data.Sqlite

## Related Decisions
- [[decisions/_index|ADR-009]] Benchmark CLI design
- [[decisions/_index|ADR-011]] Optimal STT pipeline settings
- [[decisions/_index|ADR-025]] Gemma 4 via llama.cpp
- [[decisions/_index|ADR-029]] Gemma 4 model download UI

## Speech Recognition Engines

### Whisper (default)
- Configured via `"whisper": { ... }` block in benchmark config JSON
- Uses `ISpeechRecognizer` → `WhisperSpeechRecognizer` from Platform
- Supports CUDA, Vulkan, and CPU runtimes (`RuntimePreference.Auto/Cuda/Vulkan/Cpu`; `Cuda`/`Vulkan` are strict per ADR-022)

### Gemma 4 (via llama.cpp)
- Configured via `"llamaCpp": { ... }` block (mutually exclusive with `"whisper"`)
- Uses `LlamaCppSpeechRecognizer` from Platform (same as Desktop)
- Fields: `modelId` (GGUF catalog ID, default `"gemma-4-E4B-it-Q4_K_M"`), `port` (default 8321), `serverFolder?`
- `InMemorySettingsService` (benchmark-scoped) feeds config values to the recognizer, bypassing the user's `settings.json`
- `LlamaCppSpeechRecognizer` resolved directly (bypasses `DelegatingSpeechRecognizer`)
- Requires llama-server + GGUF model pre-downloaded via Desktop Settings
- `--gpu false` flag is a no-op for llama.cpp (GPU controlled by `-ngl` inside the recognizer)

