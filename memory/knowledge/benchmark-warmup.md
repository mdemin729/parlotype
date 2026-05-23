---
title: Benchmark Warm-up Pass
type: knowledge
tags: [benchmark, warm-up, cold-start, cuda, gemma4, whisper]
created: 2026-05-23
summary: Every benchmark run performs one throwaway transcription on the first sample to eliminate cold-start noise; reported separately as warmupTimeMs.
---

# Benchmark Warm-up Pass

Every `benchmark run` performs **one throwaway `TranscribeAsync`** on the first
sample after `InitializeAsync` and before the timed loop. This primes:

- **OS page cache** for the GGUF / GGML weights file (multi-GB reads).
- **CUDA driver DLLs + JIT kernel compilation** on first inference.
- **`llama-server.exe` startup** (for Gemma 4 runs).

The same sample is re-run inside the timed loop, so all reported per-sample
numbers are warm. Reported separately as `BenchmarkSummary.WarmupTimeMs`
(nullable — old imported runs show `—`). `SqliteResultIndex.runs.warmup_ms` is
added via an idempotent `PRAGMA table_info` migration.

## Why this matters

`gemma-4-E2B-it-Q8_0` on LibriSpeech test-other showed ~21.3 s "model load"
before its first sample (cold OS cache + first CUDA load + first
`llama-server` start), while a later run of the larger `gemma-4-E4B-it-BF16`
model loaded in ~6.7 s on the same hardware — purely because earlier runs had
warmed the cache and CUDA driver.

## Design rules (ADR 031)

- **Always on** — no config/CLI flag.
- **Fail loud** — warm-up exceptions propagate; a failed warm-up means the
  timed loop would still be cold.
- **GC baselines move** — captured *after* warm-up so per-sample GC counters
  reflect the timed loop only.
- **`modelLoadTimeMs` unchanged** — remains pure `InitializeAsync` time.
- **`peakRamMb` caveat** — `Process.PeakWorkingSet64` is a high-water mark;
  Whisper warm-up RAM may bleed in, but Gemma's model lives in an external
  process and isn't counted either way.

## Historical comparability

The 8 LibriSpeech test-other runs created before ADR 031 are cold-start
baselines. WER/CER/per-sample RTF remain comparable against new warm runs;
`modelLoadTimeMs` is not directly comparable (post-warmup runs report only
`InitializeAsync`, pre-warmup runs include first-inference cold I/O).

## References

- ADR: [[../decisions/031-benchmark-warmup-pass]]
- Service: [[../services/benchmark]]
- Code: `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` (warm-up block);
  `src/Parlotype.Benchmark/Results/SqliteResultIndex.cs` (schema + migration)
