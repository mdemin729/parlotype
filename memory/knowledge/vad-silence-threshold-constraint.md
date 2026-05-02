---
title: VAD Chunking and Silence Threshold Constraint
type: knowledge
status: active
tags: [audio, vad, pipeline, silence-detection]
created: 2026-05-01
summary: AudioPipelineService processes VAD in 500ms chunks; silence threshold must be ≥ 500ms or unprocessed audio is mistaken for silence
---

# VAD Chunking and Silence Threshold Constraint

## The Constraint

`AudioPipelineService.ProcessBatch()` runs Silero VAD only when at least `VadMinChunkSamples` (8,000 samples = 500ms at 16kHz) of new audio has accumulated. However, the silence-after-speech check (`silenceAfterSpeech >= _silenceThresholdSamples`) runs on **every audio callback** using the full `_sampleBuffer.Count`.

This means audio that arrives *after* the last VAD-processed position but *before* the next VAD chunk threshold is counted as "silence" — even though the VAD hasn't analyzed it yet. If the silence threshold is below 500ms, unprocessed audio will be mistaken for silence and the pipeline will flush mid-speech.

## Impact

`WaitTimeOption` values below `Medium` (500ms) — `Instant` (100ms), `VeryShort` (200ms), `Short` (300ms) — are automatically clamped to `VadMinChunkSamples` in `CacheSettingsAsync()`:

```csharp
_silenceThresholdSamples = Math.Max(rawSamples, VadMinChunkSamples);
```

## Why This Matters

This interaction is non-obvious because:
1. The silence check and the VAD refresh run at different cadences
2. `_sampleBuffer.Count` grows continuously (every ~10ms callback), but `_vadProcessedUpTo` jumps in 500ms steps
3. The gap between them looks like "silence" to the threshold check

## Future Considerations

To truly support sub-500ms silence thresholds, the VAD chunking strategy would need to change — either smaller VAD windows (at the cost of more frequent Silero inference) or computing silence only from VAD-processed audio.
