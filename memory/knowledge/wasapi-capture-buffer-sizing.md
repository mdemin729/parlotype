---
type: knowledge
tags: [naudio, wasapi, audio, allocations, loh]
created: 2026-07-13
summary: NAudio WASAPI BytesRecorded is a byte count of the device-native format — sizing float buffers from it over-allocates ~4× into the LOH; callbacks are sequential so pooled buffers are safe
---

# NAudio / WASAPI capture buffer sizing

- `WaveInEventArgs.BytesRecorded` counts **bytes of the device-native format**
  (WASAPI shared mode is typically IEEE-float32 stereo 48 kHz ≈ 384 000 B/s).
  Using it as a *float count* allocates 4× the byte size — 76.8–153.6 KB per
  50–100 ms callback, crossing the 85 000-byte LOH threshold ⇒ ~1.5 MB/s of
  Gen2-collected garbage for the whole duration of a recording. Measured:
  15.4 MB per 10 s of simulated capture vs 0 B pooled
  (`CaptureBufferBenchmarks`, ADR-045).
- After resampling to 16 kHz mono the actual output is ~24× smaller than the
  allocation (~1 600 floats per 100 ms).
- NAudio raises `DataAvailable` **sequentially from its capture thread**, so a
  rented/reused buffer is safe as long as subscribers copy synchronously —
  that lifetime contract is now documented on
  `Parlotype.Core.Audio.AudioDataEventArgs.Buffer` and regression-tested.
- `BufferedWaveProvider.DiscardOnBufferOverflow = true` **silently drops
  audio** when the consumer is slower than the buffer duration — never run
  inference (VAD) on the capture callback thread (why the pipeline moved to a
  segmenter task, ADR-045).
- **2026-07-14 correction:** the capture buffer allocation was *not* the
  dominant recording-time allocator — the NAudio resampler's per-`Read`
  allocation proportional to the *requested count* was (~27 MB/s at a
  `BytesRecorded`-sized request). See [[naudio-resampler-read-cost]] /
  ADR-045 amendment: right-size the read request (~2× expected output),
  never pass a pooled array's `.Length`.
