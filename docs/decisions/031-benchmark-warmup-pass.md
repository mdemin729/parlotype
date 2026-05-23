# ADR 031: Always-on benchmark warm-up pass

**Status:** Accepted
**Date:** 2026-05-23

## Context

Cold-start costs noticeably inflate the first sample of every benchmark run:

- OS page cache is cold for the GGUF / GGML weights file (multi-GB reads).
- CUDA driver loads its DLL and JIT-compiles kernels on first inference.
- For Gemma, the external `llama-server.exe` process starts and loads weights.

Concretely, running `gemma-4-E2B-it-Q8_0` on LibriSpeech test-other showed
~21.3 s "model load" before its first sample, while a later run of the larger
`gemma-4-E4B-it-BF16` model on the same hardware loaded in ~6.7 s — purely
because the OS file cache and CUDA had already been warmed by the earlier run.

This means:

- `modelLoadTimeMs` mixes `InitializeAsync` work with file I/O that's really
  paid by the *first inference*.
- The first sample's per-sample time / RTF is also distorted.
- Cross-run comparisons (and CI regression checks) are noisier than necessary.

## Decision

Every benchmark run performs **one throwaway "warm-up" transcription** of the
first sample between `InitializeAsync` and the timed sample loop. The result
is discarded and the elapsed time is recorded in a new `warmupTimeMs` field.

### Specifics

- **Always on.** No config flag, no CLI flag. Reproducibility is more valuable
  than a 1-sample-time saving.
- **Same sample.** The warm-up uses `allSamples[0]` (after `--tags` / `--samples`
  filtering); the timed loop re-runs the same sample, so all reported per-sample
  numbers are warm.
- **Raw `TranscribeAsync`** is used — not the VAD pipeline simulator. The goal
  is to prime CUDA / OS cache / first-inference paths, not to mirror VAD
  preprocessing.
- **Fail loud.** Warm-up exceptions propagate and fail the run. Swallowing them
  would leave the timed loop cold, defeating the feature.
- **GC baselines move.** `GC.CollectionCount()` baselines are captured *after*
  warm-up so the per-sample GC counters reflect the timed loop only.
- **`modelLoadTimeMs` semantics unchanged** — it remains the pure
  `InitializeAsync` time. Warm-up is reported separately so the two
  components remain comparable.
- **`peakRamMb` caveat.** `Process.PeakWorkingSet64` is an OS high-water mark,
  so warm-up RAM may be reflected in peak readings. For Whisper the model is
  already loaded by `InitializeAsync` so this is a non-issue. For Gemma the
  model lives in an external `llama-server` process and is not counted either
  way. Documented in the benchmark README.

### Schema and reporting

- `BenchmarkSummary.WarmupTimeMs` is **nullable** so old JSON results imported
  via `benchmark import` remain distinguishable from a real zero (reports show
  `—`, comparer skips the delta).
- `SqliteResultIndex` adds a nullable `warmup_ms REAL` column. Migration uses
  `PRAGMA table_info(runs)` + conditional `ALTER TABLE ADD COLUMN`.
- Console / Markdown reports add a "Warm-up" row. CSV sweep summary adds a
  `WarmupMs` column.
- `ComparisonResult.WarmupDelta` is **non-required** (nullable); populated only
  when both runs have warm-up data.

## Consequences

**Positive**

- `modelLoadTimeMs` becomes a clean measure of `InitializeAsync` cost.
- Per-sample times reflect steady-state inference.
- Run-to-run variance drops, making `benchmark check` thresholds tighter.

**Negative**

- Every run pays an extra sample of inference time. Negligible for short
  samples; for very long single-sample runs the overhead is noticeable but
  still small relative to total processing time.
- The 8 historical LibriSpeech runs created before this change will not have
  warm-up data and stand as a cold-start baseline. Future runs are not
  directly comparable against them on `modelLoadTimeMs`, but `avg_wer` /
  `avg_cer` / per-sample RTF remain comparable (they're already warm in the
  historical runs apart from the first sample).
