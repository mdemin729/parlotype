---
title: Migrate Benchmark to Gemma 4 via llama.cpp
status: completed
created: 2026-05-21
started: 2026-05-21
completed: 2026-05-21
---

# Migrate Benchmark to Gemma 4 via llama.cpp

## Problem

The benchmark references `Parlotype.Gemma4` — a Python sidecar integration from the original
Gemma 4 prototype (2026-05-08). The Desktop/Platform layer has since been rewritten to use
`LlamaCppSpeechRecognizer` (llama-server process, GGUF files). Benchmark results therefore
don't reflect the production engine.

## Approach

- Replace `Parlotype.Gemma4` project reference with a DI-resolved `LlamaCppSpeechRecognizer`
- Add `InMemorySettingsService` (benchmark-scoped) so the recognizer reads config from the
  benchmark JSON file rather than the user's personal `settings.json`
- New `LlamaCppConfig` benchmark config class (fields: `modelId`, `port`, `serverFolder?`)
- JSON key `"llamaCpp"` replaces `"gemma4"` in benchmark config files
- Delete `Parlotype.Gemma4` project entirely (user confirmed)

## Workplan

- [x] Create plan file
- [x] Add `LlamaCppConfig` class to `BenchmarkConfig.cs`; update `BenchmarkConfig` computed properties
- [x] Create `InMemorySettingsService` in `Pipeline/`
- [x] Update `Program.cs` — wiring, GPU override warning, dispose
- [x] Remove `Parlotype.Gemma4` csproj references (benchmark + benchmark.tests)
- [x] Rename `Gemma4ConfigTests.cs` → `LlamaCppConfigTests.cs` and rewrite tests
- [x] Update dataset config files (gemma4-smoke-test-config.json, gemma4-libri-speech-test-other-config.json)
- [x] Remove `Parlotype.Gemma4` from `Parlotype.slnx` and delete the directory
- [x] Build clean, tests pass (94 benchmark + 254 platform)
