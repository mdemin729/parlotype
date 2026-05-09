---
title: Parlotype.Benchmark
type: service-profile
status: active
tags: [benchmark, cli, wer, cer, quality]
criticality: medium
last_updated: 2026-03-28
summary: Console CLI for evaluating transcription quality with WER/CER/RTF metrics
---

# Parlotype.Benchmark

## Purpose
Command-line tool for measuring and tracking transcription quality. Supports historical runs, comparisons, parameter sweeps, repetition stability analysis, and CI regression detection.

## Key Paths
- `src/Parlotype.Benchmark/Configuration/` — config models, sweep expansion
- `src/Parlotype.Benchmark/Metrics/` — WER/CER/RTF calculators, text normalization
- `src/Parlotype.Benchmark/Pipeline/` — benchmark execution pipeline
- `src/Parlotype.Benchmark/Results/` — SQLite index, result models
- `src/Parlotype.Benchmark/Reporting/` — CSV/Markdown/JSON exporters

## CLI Commands
```bash
run       # Execute benchmark with config
list      # List historical runs
compare   # Compare two runs (delta metrics)
export    # Export run (csv, markdown, json)
import    # Rebuild SQLite index from JSON
sweep     # Cartesian product parameter sweep
check     # CI regression detection
```

## Conventions
- Use Spectre.Console for output, never `Console.WriteLine`
- Results auto-indexed into `benchmarks.db` SQLite after each run
- Sweeps use `SweepConfig` with dot-notation axes (e.g., `whisper.model`, `whisper.beamSize`)
- Repetitions: `repetitions > 1` for mean/stddev stability analysis

## Dependencies
- [[platform]], [[core]], Parlotype.Gemma4
- System.CommandLine, Spectre.Console, Microsoft.Data.Sqlite

## Related Decisions
- [[decisions/_index|ADR-009]] Benchmark CLI design
- [[decisions/_index|ADR-011]] Optimal STT pipeline settings
- [[decisions/_index|ADR-024]] Gemma 4 Python sidecar

## Speech Recognition Engines

### Whisper (default)
- Configured via `"whisper": { ... }` block in benchmark config JSON
- Uses `ISpeechRecognizer` → `WhisperSpeechRecognizer` from Platform
- Supports CUDA, Vulkan, and CPU runtimes

### Gemma 4 (via Python sidecar)
- Configured via `"gemma4": { ... }` block (mutually exclusive with `"whisper"`)
- Uses `ISpeechRecognizer` → `Gemma4SpeechRecognizer` from Parlotype.Gemma4
- Auto-managed Python FastAPI sidecar on localhost
- Supports E2B (~2B params) and E4B (~4.5B params) models
- Quantization: `4bit` (default), `8bit`, `none` (BF16)
- Requires pre-downloaded model via `hf download`
