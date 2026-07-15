---
status: accepted
date: 2026-07-13
---

# 045. Audio Pipeline Allocation & Threading Rework

## Context

An allocation review (plans/2026-07-11-audio-pipeline-perf-security,
research.md) found the recording hot path generating ~1.5 MB/s of garbage,
much of it Large-Object-Heap sized: the WASAPI callback allocated a fresh
`float[e.BytesRecorded]` per callback (a *byte* count used as a *float*
count — ~24× oversized, 77–154 KB each), `WavEncoder` allocated 2× the WAV
size with a per-sample `BinaryWriter` write, the default Parakeet engine
duplicated every utterance buffer, and streaming mode double-copied each
window. Structurally, Silero VAD inference ran on NAudio's capture callback
thread inside a lock — with `DiscardOnBufferOverflow = true`, a slow
inference silently drops audio — and the transcription loop polled a
`ConcurrentQueue` every 50 ms.

## Decision

**Allocation (measured, ADR-044 benchmarks):**

- Capture callback rents from `ArrayPool<float>` and returns after event
  dispatch (15.4 MB → 0 B per 10 s simulated capture, no Gen2 churn).
  `AudioDataEventArgs.Buffer` is now contractually valid *only during the
  event*; subscribers must copy synchronously (regression-tested).
- `WavEncoder` rewritten: exact-size array + `BinaryPrimitives` +
  `MemoryMarshal.Cast` sample loop — 6–8× faster, ½ the allocation,
  byte-identical output (frozen-legacy equivalence tests).
- `ParakeetSpeechRecognizer` passes whole-array utterance buffers zero-copy
  via `MemoryMarshal.TryGetArray` (sherpa-onnx only reads them).
- Sample buffer pre-sized once at start (`EnsureCapacity`, Clear keeps
  capacity) + span `AddRange` bulk append; streaming windows extracted with a
  single span-slice copy. Note: `AddRange` *without* pre-sizing allocates
  more than the old per-sample loop — the pair ships together.

**Threading (behaviour-preserving):**

Three single-threaded stages joined by unbounded channels:
capture callback (RMS + pooled copy only) → segmenter task (sole owner of
the sample buffer; VAD/segmentation with unchanged thresholds and cadence:
`VadMinChunkSamples` 8 000, silence threshold from settings, 30 s cap, 64 ms
merge tolerance) → transcription task (`ReadAllAsync`, no polling). Shutdown
is channel completion: StopAsync completes the raw writer, the segmenter
drains + final-flushes + completes the utterance writer, the transcription
loop drains — same drain-on-stop semantics and 30 s timeout as before. The
buffer lock is gone (single owner); a VAD failure logs and discards the
buffer instead of killing the capture callback.

## Consequences

- Steady-state recording no longer allocates on the capture path; utterance
  latency loses the up-to-50 ms polling penalty; slow machines can no longer
  lose audio to VAD stalls.
- The pooled-buffer contract is a real constraint on `DataAvailable`
  subscribers (copy synchronously, never store) — documented in Core XML docs
  and enforced by test; violating it is a use-after-return bug.
- Utterance ordering is preserved by construction (single-reader channels);
  covered by new ordering/drain tests.
- Verified: 870 tests green; WavEncoder byte-equivalence; benchmark tables in
  the plan folder. Live GC-counter run (`dotnet-counters` during real
  dictation) pending a desktop session.

## Amendment (2026-07-14): resampler read-request sizing

The live `dotnet-counters` verification found recording allocation **rose**
from ~30 MB/s (pre-rework) to ~38–40 MB/s. A/B harnesses (real pipeline +
Silero VAD on both branches — allocation-identical at 268 KB per audio-second)
isolated the cause to the capture service: NAudio's resampler chain
**allocates per `Read` call in proportion to the requested count**
(2.73 MB/callback at a 38,400-sample request; the pool's power-of-two bucket
rounding inflated the pooled version's request to 65,536 → 4.68 MB/callback).
The pre-rework `BytesRecorded`-sized request was itself the *dominant*
allocator during recording all along — not the capture buffer this ADR
originally targeted.

Fix: `WasapiAudioCaptureService` now requests only ~2× the expected resampled
output (`inputFrames × 16000 / nativeRate`, min 1,024) and passes that count —
never the rented array's `.Length` — to `Read`. Measured: **0.19 MB/callback
(~1.9 MB/s while recording), ~14× below the pre-rework baseline**, identical
sample delivery. Reproducible via `ResamplerReadBenchmarks`; details in
`memory/knowledge/naudio-resampler-read-cost.md`.
