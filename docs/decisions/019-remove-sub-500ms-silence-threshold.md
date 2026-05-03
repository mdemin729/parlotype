---
status: accepted
date: 2026-05-02
---

# 019. Remove Sub-500ms Silence Threshold Options

## Context

`WaitTimeOption` exposed 7 values (100ms–3000ms) controlling how long `AudioPipelineService` waits after speech ends before flushing audio to Whisper. However, values below 500ms (Instant=100ms, VeryShort=200ms, Short=300ms) were silently clamped to 500ms at runtime because the pipeline runs Silero VAD in 500ms chunks (`VadMinChunkSamples = 8000` samples at 16kHz). Unprocessed audio between VAD chunks was mistaken for silence, causing premature mid-speech flushing.

A benchmark pipeline simulation (954 transcriptions across 53 samples × 6 thresholds × 3 repetitions with CUDA) was conducted to measure the actual impact of removing the clamping. Results:

| Threshold | Avg WER | Verdict |
|-----------|---------|---------|
| 100ms     | 77.3%   | Catastrophic — fragments words mid-utterance |
| 200ms     | 80.6%   | Catastrophic |
| 300ms     | 79.0%   | Catastrophic |
| 500ms     | 19.7%   | Production quality |
| 1000ms    | 18.1%   | Excellent |
| 3000ms    | 17.1%   | Diminishing returns |

Sub-500ms thresholds cause Whisper to receive audio fragments, producing hallucinated output with >100% WER on some samples. The clamping was a safety mechanism, not a bug.

## Decision

1. **Remove** `Instant`, `VeryShort`, and `Short` from `WaitTimeOption` enum. The minimum is now `Medium` (500ms).
2. **Remove** the clamping logic (`Math.Max(rawSamples, VadMinChunkSamples)`) from `AudioPipelineService.CacheSettingsAsync()` since all remaining values are ≥ 500ms.
3. **Migrate** users with legacy settings: `SpeechSettingsViewModel` detects invalid enum values and rewrites them to `Medium`. `AudioPipelineService` falls back to `Medium` via `Enum.TryParse` failure.
4. **Add** `PipelineSimulator` to the benchmark infrastructure for simulating real-time pipeline flush behavior with configurable `VadConfig.SilenceThresholdMs`. This enables future data-driven evaluation of threshold changes.

Alternative considered: keeping the values but relabeling them or showing warnings. Rejected because the options would still produce 77%+ WER if the clamping were ever removed, and the labels would be confusing.

## Consequences

- **Simpler pipeline**: no clamping logic, no mismatch between displayed and actual threshold.
- **Fewer choices**: users have 4 options (500ms, 1s, 2s, 3s) instead of 7. This is sufficient for the use case.
- **Breaking change**: users with `"Instant"`, `"VeryShort"`, or `"Short"` in `settings.json` are silently migrated to `Medium`. Since these were already clamped to 500ms, there is no behavioral change.
- **Benchmark capability**: `PipelineSimulator` enables future threshold experiments without modifying production code.
- **Future work**: to support sub-500ms thresholds, the VAD chunking strategy would need to change (smaller VAD windows or silence detection from VAD-processed audio only).
