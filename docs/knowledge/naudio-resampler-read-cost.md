---
title: "NAudio / WASAPI capture allocation traps"
area: "audio"
summary: "NAudio's resampler allocates per Read in proportion to the requested count, and BytesRecorded counts native-format bytes; request ~2x the expected output."
verified: 2026-11-11
---

# NAudio / WASAPI capture allocation traps

Measured with NAudio 2.2.1 while working on
[ADR-045](../decisions/045-audio-pipeline-allocation-threading.md).

## Resampler: the Read-request size drives per-call allocation

The `BufferedWaveProvider → ToSampleProvider → WdlResamplingSampleProvider → ToMono` chain
(used by `WasapiAudioCaptureService`) **allocates managed memory on every `Read` call in
proportion to the count you request**, regardless of how many samples actually come back.
One 100 ms callback of 48 kHz stereo float32 (38,400 bytes in, 1,600 mono samples out):

| Read request (floats) | Allocated per call | While recording (10 cb/s) |
|----------------------:|-------------------:|--------------------------:|
| 38,400 (`BytesRecorded`) | 2.73 MB | ~27 MB/s |
| 65,536 (ArrayPool bucket) | 4.68 MB | ~47 MB/s |
| 3,200 (2× expected output) | 0.19 MB | **~1.9 MB/s** |

All variants return the same samples; nothing is lost with the small request. The estimate
is deterministic (`inputFrames × targetRate / nativeRate`) and 2× slack lets one read
consume everything available.

Two traps this creates:

1. **Never pass `BytesRecorded` (or anything derived from input size) as the read count.**
   Output after 48 kHz stereo → 16 kHz mono is ~24× smaller. This was the dominant allocator
   during recording, dwarfing the capture buffer itself.
2. **Never pass `rentedArray.Length` as the read count.** `ArrayPool.Rent` rounds up to a
   power-of-two bucket, silently multiplying the resampler's internal allocations. This
   caused a +70 % allocation regression when the capture buffer was first pooled (found via
   `dotnet-counters` on a live dictation).

Reproducible in-repo: `ResamplerReadBenchmarks` in `Parlotype.MicroBenchmarks`.

## Capture buffer sizing

- `WaveInEventArgs.BytesRecorded` counts **bytes of the device-native format** (WASAPI
  shared mode is typically IEEE-float32 stereo 48 kHz ≈ 384,000 B/s). Using it as a *float
  count* allocates 4× the byte size: 76.8–153.6 KB per 50–100 ms callback, over the
  85,000-byte LOH threshold, so ~1.5 MB/s of Gen2-collected garbage for the whole recording.
  Measured: 15.4 MB per 10 s of simulated capture vs 0 B pooled (`CaptureBufferBenchmarks`).
- NAudio raises `DataAvailable` **sequentially from its capture thread**, so a rented or
  reused buffer is safe as long as subscribers copy synchronously. That lifetime contract is
  documented on `Parlotype.Core.Audio.AudioDataEventArgs.Buffer`.
- `BufferedWaveProvider.DiscardOnBufferOverflow = true` **silently drops audio** when the
  consumer is slower than the buffer duration. Never run inference (VAD) on the capture
  callback thread; that is why the pipeline moved to a segmenter task.
