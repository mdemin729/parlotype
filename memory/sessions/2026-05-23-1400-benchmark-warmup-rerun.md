---
title: "Session: 2026-05-23 — Benchmark warm-up pass + LibriSpeech test-other re-run"
type: session
status: complete
tags: [benchmark, warmup, librispeech, gemma4, whisper]
created: 2026-05-23
summary: "Added always-on benchmark warm-up pass (ADR 031), created 7 new LibriSpeech test-other configs, and re-ran all 8 benchmarks to produce a fresh comparison report."
---

# Session: 2026-05-23 — Benchmark warm-up pass + LibriSpeech test-other re-run

## Active Focus
- Created 7 missing benchmark configs for `datasets/libri-speech-test-other` (Whisper Small/Medium/LargeV3Turbo + Gemma-4 E2B/E4B BF16/Q4/Q8 variants); renamed pre-existing E4B-Q4 config to match the new naming pattern.
- Implemented always-on warm-up pass in `BenchmarkRunner` between `InitializeAsync` and the timed loop. GC baselines moved to post-warm-up so per-sample GC counters reflect only timed work.
- Plumbed `WarmupTimeMs` (nullable `double?`) through `BenchmarkSummary`, `SqliteResultIndex` (with idempotent `ALTER TABLE` migration via `PRAGMA table_info(runs)`), `ConsoleReporter`, `MarkdownFormatter`, `CsvFormatter`, `ComparisonResult.WarmupDelta`, and `ResultComparer`.
- Ran all 8 LibriSpeech test-other benchmarks with warm-up active; produced `results/comparison-libri-speech-test-other-2026-05-23-warmup.md`.

## Decisions Made
- **ADR 031**: warm-up pass is always on (no flag), runs sample[0] once, fails loud on exceptions. Rationale: every run should have comparable cold-start handling and the cost is bounded (≤2 s).
- `BenchmarkResult`/`BenchmarkSummary` remain `sealed class` (not records) — tests use updated factory helpers with a `warmupTimeMs` parameter instead of `with` syntax.
- `WarmupDelta` on `ComparisonResult` is optional/nullable (not `required`) to avoid breaking existing test object initializers.
- `modelLoadTimeMs` is retained but now measures only `InitializeAsync` post-warm-up; the previous cold-start variance is gone (verified: E2B-Q8_0 dropped 21.3 s → 6.7 s).

## Facts Learned
- `gemma-4-E2B-it-Q8_0` is unstable on this dataset: intermittently emits stray `<|channel>` tokens during reasoning that crash llama-server's chat-template parser with HTTP 500. Required a retry to get a clean 50-sample run; resulting RTF was anomalously high (0.315 vs ~0.04) due to verbose reasoning bleed-through.
- Whisper greedy decoder is deterministic — WER/CER values matched the pre-warm-up run exactly across all 3 Whisper configs.
- `EnvironmentInfo.WhisperRuntime` is `"unknown"` for llama.cpp/Gemma runs in SQLite — not populated for that engine.
- `Process.PeakWorkingSet64` is a high-water mark, so per-sample RAM tracking may now include warm-up RAM for Whisper. For Gemma the model lives in the `llama-server.exe` child process and isn't counted either way — documented in README + ADR.
- Avalonia headless `CaptureRenderedFrame` requires `UseSkia()` + `UseHeadlessDrawing=false` (unrelated, but verified incidentally).

## Open Blockers
- None blocking. Optional follow-ups:
  - Investigate `gemma-4-E2B-it-Q8_0` `<|channel>` instability — likely a sampler/prompt tuning fix.
  - Wire external-process RAM tracking so Gemma peak memory is meaningful.
  - Populate `EnvironmentInfo.WhisperRuntime` (or rename it) for llama.cpp runs.

## Documentation Status
- ADR: done — `docs/decisions/031-benchmark-warmup-pass.md`
- Vault (services/architecture): done — `memory/services/benchmark.md` updated, ADR row in `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/benchmark-warmup.md`

## Next Action
Session complete. If resumed: consider tuning `gemma-4-E2B-it-Q8_0` sampler/prompt to suppress the `<|channel>` reasoning token, or add external-process RAM tracking so Gemma `peakRamMb` reports actual model RAM rather than the orchestrator process footprint.
