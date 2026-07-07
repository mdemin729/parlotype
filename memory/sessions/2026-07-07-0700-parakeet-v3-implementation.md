---
title: "Session: Parakeet v3 engine implementation"
type: session
status: completed
tags: [parakeet, sherpa-onnx, speech-engine, implementation]
created: 2026-07-07
summary: "Implemented the full Parakeet TDT 0.6B v3 engine plan (ADR-041): spike, Core contracts, Platform recognizer + downloader, Desktop UI, Benchmark integration, tests, docs."
---

# Session: Parakeet v3 engine implementation

## Active Focus

Executed all 6 phases of `plans/2026-07-06-parakeet-v3-engine/implementation-plan.md`:

- **Spike** (scratchpad): `org.k2fsa.sherpa.onnx` 1.13.3 builds warning-free on net10.0; en/de/fr test WAVs transcribed correctly with punctuation; RTF 0.10–0.13, load ~2.9 s / 715 MB
- **Core**: `SpeechEngine.Parakeet`, `ParakeetModelInfo` (per-model cache subdir), `SettingsKeys.SelectedParakeetModel`, `LanguageCatalog.ParakeetLanguages` (25, filtered from Whisper list), `SpeechEngineCapabilities` Parakeet branch (`TranslationForm.None`)
- **Platform**: `ParakeetSpeechRecognizer` (OfflineRecognizer, `nemo_transducer`, greedy, Task.Run load/decode, init lock), `ParakeetModelDownloadService` (4-file HF download, cumulative progress, delete), factory switch + DI
- **Desktop**: third engine card, `ParakeetModelSettingsViewModel`/View (RestrictToEngine=Parakeet), `ParakeetModelDownloadDialogService`, `ModelDownloadViewModel.ForParakeetModel`, `SettingsWindowViewModel` + DataTemplate wiring, `EngineName` case in `LanguageRelationshipViewModel`
- **Benchmark**: `ParakeetConfig` section (mutually exclusive with whisper/llamaCpp), headless auto-download, `LanguageDisplay`/`BeamSizeDisplay` helpers replacing IsLlamaCpp ternaries, `datasets/parakeet-smoke-config.json`
- **Tests**: 727 green (379 Tests + 238 Desktop.Tests + 110 Benchmark.Tests); new Parakeet tests in all three projects; updated `EngineOptions_ContainsThreeEntries` + `SettingsWindowViewModelTests.BuildViewModel` + Parakeet nav test
- **Docs**: ADR-041, vault (core/platform/desktop/benchmark profiles, subsystems Speech Engines table, decisions index), knowledge `sherpa-onnx-quirks`, CLAUDE.md overview

## Decisions Made

- Model files live in `models/<modelId>/` subdirectories because upstream names are generic (`encoder.int8.onnx`)
- Benchmark auto-downloads the Parakeet model headlessly when missing (matches Whisper UX, unlike Gemma)
- Reporting display logic centralized in `BenchmarkConfig.LanguageDisplay`/`BeamSizeDisplay` instead of scattering three-way ternaries

## Facts Learned

Distilled to [[sherpa-onnx-quirks]]: NuGet CPU-only, config uses public fields,
no confidence/lang in NeMo results, auto-resample logs to stderr, synchronous
load/decode.

## Measured (smoke-test, 3 samples, 16-core CPU)

| Engine | WER | CER | RTF | Load | RAM |
|--------|----:|----:|----:|-----:|----:|
| Parakeet v3 INT8 (CPU) | 5.6 % | 2.5 % | 0.072 | 3.3 s | 918 MB |
| Whisper Base (Vulkan GPU) | 3.6 % | 1.8 % | 0.032 | 0.9 s | 431 MB |

## Open Blockers

None. Deferred (documented in task.md): language-page hint that Parakeet
ignores an explicitly selected source language; manual in-app dictation pass
(engine path verified via benchmark, UI via headless tests).

## Documentation Status

- ADR: done — `docs/decisions/041-parakeet-v3-sherpa-onnx.md`
- Vault: done — services/core+platform+desktop+benchmark, architecture/subsystems, decisions/_index
- Knowledge: done — `memory/knowledge/sherpa-onnx-quirks.md` + index row

## Next Action

Optional follow-ups: manual dictation smoke test in the running app (Settings →
Engine → Parakeet, download, hotkey dictate); add the "always auto-detected"
source hint on the Language page for Parakeet; consider a fp32 catalog entry if
INT8 accuracy on accented speech (16.7 % WER sample) proves limiting.
