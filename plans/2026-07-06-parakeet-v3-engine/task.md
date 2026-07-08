---
title: Parakeet TDT 0.6B v3 speech engine (sherpa-onnx)
status: completed
created: 2026-07-06
started: 2026-07-06
completed: 2026-07-07
---

# Parakeet TDT 0.6B v3 speech engine

## Problem

Whisper is Parlotype's default engine but is comparatively slow, especially on
machines without an NVIDIA GPU. NVIDIA's Parakeet TDT 0.6B v3 transcribes 25
European languages with punctuation/capitalization, beats Whisper large-v3 on
accuracy, and runs many× real-time **on CPU** with an INT8 ONNX build — a
~670 MB download vs ~6 GB for Gemma 4. Parlotype has no engine that is both
fast and GPU-independent.

## Approach

Add `SpeechEngine.Parakeet` as a third engine, powered **in-process** by the
official sherpa-onnx .NET bindings (`org.k2fsa.sherpa.onnx` NuGet,
`OfflineRecognizer` with `model_type=nemo_transducer`) — no sidecar process.
Model files (encoder/decoder/joiner INT8 ONNX + tokens.txt, ~670 MB total) are
downloaded per-file from HuggingFace
(`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8`) into the existing
`%LOCALAPPDATA%\parlotype\models\` cache, mirroring the Gemma 4 download
pattern (ADR-029) including cumulative progress.

Key characteristics baked into the capability model:
- transcribe-only → `TranslationForm.None` (the UI fallback branch in
  `SpeechEngineCapabilities` was written for exactly this case)
- always auto-detects language (no way to force a source language) — 25
  European languages
- CPU-only in v1 (no pre-built CUDA NuGet exists); that is the feature, not a
  gap — it gives AMD/Intel users a fast local engine

Whisper stays the default engine. Full details: [research.md](research.md) and
[implementation-plan.md](implementation-plan.md).

## Workplan

- [x] **Spike:** minimal console test — NuGet package + int8 model transcribes a
      test WAV; confirm zero-warning build under `TreatWarningsAsErrors`
      (en/de/fr all correct with punctuation; RTF 0.10–0.13; load ~2.9 s / 715 MB)
- [x] Core: add `SpeechEngine.Parakeet`, `ParakeetModelInfo` catalog,
      `SettingsKeys.SelectedParakeetModel`, `LanguageCatalog.ParakeetLanguages`
- [x] Core: `SpeechEngineCapabilities` branch for Parakeet (auto-detect, 25
      source languages, no translation)
- [x] Platform: `ParakeetSpeechRecognizer : ISpeechRecognizer` (sherpa-onnx
      `OfflineRecognizer`, load/unload lifecycle, `Task.Run` decode)
- [x] Platform: `ParakeetModelDownloadService` (4-file HF download, cumulative
      progress, delete support); DI registration in `PlatformServiceExtensions`
- [x] Platform: extend `SpeechRecognizerFactory` switch
- [x] Desktop: engine card in `SpeechEngineSettingsViewModel`; new
      `ParakeetModelSettingsViewModel` + view (RestrictToEngine=Parakeet,
      download/delete dialog)
- [x] Benchmark: `ParakeetConfig` in `BenchmarkConfig` + pipeline wiring so
      WER/CER/RTF can be compared against Whisper; run smoke dataset
      (Parakeet: WER 5.6 % / RTF 0.072 CPU; Whisper Base: 3.6 % / 0.032 GPU)
- [x] Tests: Core (capabilities, catalog), Platform (path resolution, lifecycle),
      Desktop.Tests (engine list, settings section, nav), Benchmark.Tests
      (config) — 727 tests green
- [x] Verify end-to-end: full transcription through `ParakeetSpeechRecognizer`
      via the benchmark pipeline (English + accented sample); spike verified
      de/fr multilingual output
- [x] ADR-041 (Parakeet via sherpa-onnx), memory vault updates
      (`memory/services/*`, `memory/architecture/subsystems.md`,
      `memory/decisions/_index.md`), knowledge note `sherpa-onnx-quirks`

## Deferred (follow-up candidates)

- ~~Language-page hint that Parakeet always auto-detects~~ — resolved by
  [2026-07-07-parakeet-default-language-ux](../2026-07-07-parakeet-default-language-ux/)
  (ADR-042): the language UI now hides entirely for Parakeet
- Manual in-app dictation pass (hotkey → injected text) — engine path is
  exercised end-to-end by the benchmark; UI wiring covered by headless tests
