---
title: "Session: 2026-05-21 — Benchmark llama.cpp migration"
type: session
status: active
tags: [benchmark, llamacpp, gemma4, migration]
created: 2026-05-21
summary: "Migrated the Parlotype Benchmark from the Python sidecar (Parlotype.Gemma4) to LlamaCppSpeechRecognizer. Deleted the Parlotype.Gemma4 project."
---

# Session: 2026-05-21 — Benchmark llama.cpp migration

## Active Focus

Replaced the old `Parlotype.Gemma4` Python-sidecar benchmark engine with the current
`LlamaCppSpeechRecognizer` (Platform project, llama-server).

Files added:
- `src/Parlotype.Benchmark/Pipeline/InMemorySettingsService.cs` — ISettingsService backed by
  a `Dictionary<string, string?>`, pre-populated from `LlamaCppConfig` in the benchmark JSON

Files modified:
- `src/Parlotype.Benchmark/Configuration/BenchmarkConfig.cs` — swapped `Gemma4Config? Gemma4`
  → `LlamaCppConfig? LlamaCpp`; added `LlamaCppConfig` record at bottom; updated `IsLlamaCpp`,
  `EngineName`, `ModelDisplayName`; removed `using Parlotype.Gemma4`
- `src/Parlotype.Benchmark/Program.cs` — DI override for `ISettingsService`, resolve
  `LlamaCppSpeechRecognizer` directly from DI, `--gpu false` emits a warning for llama.cpp;
  removed `Gemma4SpeechRecognizer` instantiation
- `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` — `IsGemma4` → `IsLlamaCpp`
- `src/Parlotype.Benchmark/Reporting/ConsoleReporter.cs` — `IsGemma4` → `IsLlamaCpp`
- `src/Parlotype.Benchmark/Reporting/CsvFormatter.cs` — `IsGemma4` → `IsLlamaCpp`
- `src/Parlotype.Benchmark/Reporting/MarkdownFormatter.cs` — `IsGemma4` → `IsLlamaCpp`,
  language label updated to "en (llama.cpp)"
- `src/Parlotype.Benchmark/Parlotype.Benchmark.csproj` — removed `Parlotype.Gemma4` reference
- `src/Parlotype.Benchmark.Tests/Parlotype.Benchmark.Tests.csproj` — same
- `src/Parlotype.Benchmark/README.md` — updated Gemma4 section to document `"llamaCpp"` block
- `datasets/gemma4-smoke-test-config.json` — migrated from `"gemma4"` to `"llamaCpp"` key
- `datasets/gemma4-libri-speech-test-other-config.json` — same migration
- `Parlotype.slnx` — removed `Parlotype.Gemma4` project entry
- `plans/2026-05-21-benchmark-llamacpp-migration.md` — created + marked completed

Files deleted:
- `src/Parlotype.Benchmark.Tests/Gemma4ConfigTests.cs` — replaced by `LlamaCppConfigTests.cs`
- `src/Parlotype.Gemma4/` — entire directory deleted (user confirmed)

Files created:
- `src/Parlotype.Benchmark.Tests/LlamaCppConfigTests.cs` — 8 tests for `LlamaCppConfig`

## Decisions Made

- `InMemorySettingsService` overrides `JsonSettingsService` after `AddPlatformServices()` call,
  so `LlamaCppSpeechRecognizer` reads benchmark-controlled port/modelId/serverFolder without
  touching the user's `%LOCALAPPDATA%/parlotype/settings.json`
- `LlamaCppSpeechRecognizer` resolved by concrete type from DI (bypasses `DelegatingSpeechRecognizer`
  which reads the `SpeechEngine` setting — we want llama.cpp unconditionally)
- `--gpu false` flag for llama.cpp is a no-op with a warning (GPU control lives inside
  `LlamaCppSpeechRecognizer.InitializeAsync` via `-ngl 99`)
- JSON key changed from `"gemma4"` to `"llamaCpp"` — clearer naming; no backward-compat concern
  since these are hand-written benchmark configs
- `Parlotype.Gemma4` project deleted entirely (no remaining references)
- Sweeps remain Whisper-only (llama.cpp params don't benefit from a sweep axis)

## Facts Learned

No new non-derivable facts; the migration followed the existing Platform architecture exactly.

## Open Blockers

None.

## Next Action

Nothing required immediately. The benchmark can now be run against Gemma 4 with:
```bash
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/gemma4-smoke-test-config.json \
  --datasets datasets --output results
```
Branch `claude/agitated-gould-c939ab` is clean; a PR to master may be desired.
