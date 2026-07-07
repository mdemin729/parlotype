# ADR-041: Parakeet TDT 0.6B v3 via sherpa-onnx

- **Status:** Accepted
- **Date:** 2026-07-07
- **Deciders:** Maksim

## Context

Whisper is Parlotype's default engine but is comparatively slow, and its GPU
acceleration (CUDA/Vulkan, ADR-012/022) doesn't help users without a capable
GPU. NVIDIA's Parakeet TDT 0.6B v3 (FastConformer encoder + Token-and-Duration
Transducer decoder, CC-BY-4.0) transcribes 25 European languages with native
punctuation/capitalization at ~6.3 % average WER on the Open ASR Leaderboard —
while decoding many times faster than real time **on CPU** with an INT8 ONNX
build. Research: `plans/2026-07-06-parakeet-v3-engine/research.md`.

## Decision

Add `SpeechEngine.Parakeet` as a third engine, powered **in-process** by the
official sherpa-onnx .NET bindings — no sidecar process.

### Key design choices

1. **`org.k2fsa.sherpa.onnx` NuGet (1.13.3)** in Platform — `OfflineRecognizer`
   with `ModelType = "nemo_transducer"`, greedy search, CPU provider.
2. **`ParakeetSpeechRecognizer`** implements `ISpeechRecognizer` — loads the
   model off-thread (ADR-038 spinner), one `OfflineStream` per transcription,
   decode wrapped in `Task.Run`. Supports `UnloadAsync` re-init (ADR-017).
3. **`ParakeetModelInfo` catalog in Core** — single entry
   `parakeet-tdt-0.6b-v3-int8` (~670 MB: encoder 652 MB + decoder 12 MB +
   joiner 6 MB + tokens), downloaded per-file from HuggingFace
   `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` by
   `ParakeetModelDownloadService` (ADR-029 pattern, cumulative progress).
   Files keep generic upstream names, so each model gets its own subdirectory
   under `%LOCALAPPDATA%\parlotype\models\<modelId>\`.
4. **CPU-only** — the pre-built sherpa-onnx packages ship no GPU provider
   (CUDA requires a source build). This is the feature, not a gap: INT8 CPU
   decoding measured RTF ≈ 0.07–0.13, and AMD/Intel users get a fast engine
   with no GPU dependency.
5. **Capabilities:** auto-detect only (the model has no language-forcing
   parameter), 25-language fixed source set
   (`LanguageCatalog.ParakeetLanguages`), no translation ⇒
   `TranslationForm.None` — the exact fallback shape the ADR-036 language UI
   was designed to render (disabled target + note).
6. **Benchmark integration** — `parakeet` config section, headless
   auto-download, `datasets/parakeet-smoke-config.json`.

### Measured results (smoke-test dataset, 3 samples, this machine: 16-core CPU)

| Engine | Avg WER | Avg CER | Avg RTF | Model load | Peak RAM |
|--------|--------:|--------:|--------:|-----------:|---------:|
| Parakeet TDT v3 INT8 (CPU) | 5.6 % | 2.5 % | 0.072 | 3.3 s | 918 MB |
| Whisper Base (Vulkan GPU) | 3.6 % | 1.8 % | 0.032 | 0.9 s | 431 MB |

The English-heavy smoke set favors Whisper Base on GPU; Parakeet's INT8 CPU
numbers are its selling point (no GPU needed), and its published multilingual
WER beats Whisper large-v3. The 16.7 % outlier was the Russian-accented sample.

## Consequences

### Positive
- Fastest no-GPU engine; ~670 MB download (vs ~6 GB Gemma 4)
- In-process — no port management, health polling, or sidecar crash handling
- Native punctuation + capitalization; automatic language detection
- Same `ISpeechRecognizer` contract — pipeline, prewarm, and hot-swap unchanged

### Negative
- CPU-only; no GPU path without building sherpa-onnx from source
- Transcribe-only: no translation task, and the source language cannot be
  forced (a selected source is informational; the model always auto-detects)
- 25 European languages only (no CJK, Arabic, etc.) — Whisper remains default
- New native dependency (sherpa-onnx + bundled onnxruntime) in the output
- sherpa-onnx C# result exposes no confidence or detected-language fields for
  NeMo transducers ⇒ `TranscriptionResult.Confidence`/`DetectedLanguage` are null

## Alternatives Considered

1. **Hand-rolled ONNX Runtime (Microsoft.ML.OnnxRuntime)** — full control and a
   DirectML GPU path, but we'd own log-mel extraction and TDT decoding;
   significant correctness risk for little v1 gain.
2. **NeMo / Python sidecar** — rejected; ADR-024/025 already demonstrated the
   sidecar cost, and a Python runtime dependency is worse than llama-server.
3. **llama.cpp** — does not support Parakeet.

## Related

- ADR-025: Gemma 4 via llama.cpp (second-engine precedent)
- ADR-029: Gemma 4 model download UI (download pattern)
- ADR-036: Language UX rebuild (`TranslationForm.None` rendering)
- Plan: `plans/2026-07-06-parakeet-v3-engine/`
