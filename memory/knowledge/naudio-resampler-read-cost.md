---
type: knowledge
tags: [naudio, resampler, allocations, audio, wasapi]
created: 2026-07-14
summary: NAudio's WDL resampler chain allocates per Read call in proportion to the REQUESTED count, not the returned count — right-size read requests to ~2× the expected output
---

# NAudio resampler: Read-request size drives per-call allocation

The `BufferedWaveProvider → ToSampleProvider → WdlResamplingSampleProvider →
ToMono` chain (used by `WasapiAudioCaptureService`) **allocates managed memory
on every `Read` call in proportion to the count you request**, regardless of
how many samples actually come back. Measured with NAudio 2.2.1, one 100 ms
callback of 48 kHz stereo float32 (38,400 bytes in, 1,600 mono samples out):

| Read request (floats) | Allocated per call | While recording (10 cb/s) |
|----------------------:|-------------------:|--------------------------:|
| 38,400 (`BytesRecorded`) | 2.73 MB | ~27 MB/s |
| 65,536 (ArrayPool bucket) | 4.68 MB | ~47 MB/s |
| 3,200 (2× expected output) | 0.19 MB | **~1.9 MB/s** |

All variants return the same samples — nothing is lost with the small request;
the estimate is deterministic (`inputFrames × targetRate / nativeRate`) and 2×
slack lets one read consume everything available.

Two traps this creates:

1. **Never pass `BytesRecorded` (or anything derived from input size) as the
   read count** — output after 48 kHz stereo→16 kHz mono is ~24× smaller.
   This was the dominant allocator during recording, dwarfing the capture
   buffer itself.
2. **Never pass `rentedArray.Length` as the read count** — `ArrayPool.Rent`
   rounds up to a power-of-two bucket, silently multiplying the resampler's
   internal allocations (this caused a +70 % allocation regression when the
   capture buffer was first pooled; found via `dotnet-counters` on a live
   dictation, 2026-07-14).

Reproducible in-repo: `ResamplerReadBenchmarks` in `Parlotype.MicroBenchmarks`.
Related: [[wasapi-capture-buffer-sizing]] (ADR-045 amendment).
