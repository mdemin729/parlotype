---
title: VAD Chunking and Silence Threshold Constraint
type: knowledge
status: active
tags: [audio, vad, pipeline, silence-detection]
created: 2026-05-01
last_updated: 2026-05-02
summary:Sub-500ms silence thresholds cause catastrophic WER (77%+). WaitTimeOption now starts at Medium (500ms) — Instant/VeryShort/Short were removed.
---

# VAD Chunking and Silence Threshold Constraint

## The Constraint

`AudioPipelineService.ProcessBatch()` runs Silero VAD only when at least `VadMinChunkSamples` (8,000 samples = 500ms at 16kHz) of new audio has accumulated. The silence-after-speech check runs on **every audio callback** using the full `_sampleBuffer.Count`.

Audio that arrives *after* the last VAD-processed position but *before* the next VAD chunk is counted as "silence" — even though the VAD hasn't analyzed it yet. Sub-500ms silence thresholds cause premature mid-speech flushing.

## Resolution (2026-05-02)

Benchmark pipeline simulation (954 transcriptions across 53 samples × 6 thresholds × 3 reps) proved sub-500ms thresholds are catastrophic:

| Threshold | Avg WER | Verdict |
|-----------|---------|---------|
| 100ms     | 77.3%   | Fragments words mid-utterance |
| 200ms     | 80.6%   | Fragments words mid-utterance |
| 300ms     | 79.0%   | Fragments words mid-utterance |
| 500ms     | 19.7%   | Production quality |
| 1000ms    | 18.1%   | Excellent |
| 3000ms    | 17.1%   | Diminishing returns |

**Action taken:** Removed `Instant` (100ms), `VeryShort` (200ms), and `Short` (300ms) from `WaitTimeOption`. The minimum is now `Medium` (500ms). The clamping logic in `CacheSettingsAsync()` was also removed since all remaining values are ≥ 500ms.

## Why Sub-500ms Fails

1. The silence check and VAD refresh run at different cadences
2. `_sampleBuffer.Count` grows continuously (every ~10ms callback), but `_vadProcessedUpTo` jumps in 500ms steps
3. The gap between them looks like "silence" to the threshold check
4. Whisper receives audio fragments and produces hallucinated output (>100% WER possible)
