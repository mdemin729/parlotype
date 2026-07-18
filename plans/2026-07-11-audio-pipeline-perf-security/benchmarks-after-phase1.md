# Phase 1 benchmark results

Run: 2026-07-13, after the allocation fixes (same machine/config as
[benchmarks-baseline.md](benchmarks-baseline.md)).

## WavEncoder (P5) — measured directly, Legacy vs rewritten production code

| Seconds | Legacy | Current | Time ratio | Legacy alloc | Current alloc | Alloc ratio |
|--------:|-------:|--------:|-----------:|-------------:|--------------:|------------:|
| 1  | 139.6 µs | **17.7 µs** | 0.13 | 62.7 KB | **31.3 KB** | 0.50 |
| 10 | 1,493 µs | **257.1 µs** | 0.17 | 625.3 KB | **312.6 KB** | 0.50 |
| 30 | 4,060 µs | **672.6 µs** | 0.17 | 1,877.6 KB | **937.6 KB** | 0.50 |

**6–8× faster, exactly ½ the allocation** (single exact-size array; the legacy
MemoryStream + `ToArray` pair is gone). Output verified byte-identical against
a frozen copy of the legacy encoder
(`WavEncoderTests.Encode_MatchesLegacyImplementation_ByteForByte`, 5 cases
incl. clipping and a 24 kHz rate).

## Changes shipped in production code (verified by baseline variant benchmarks)

| Finding | Change | Baseline-measured effect of the chosen variant |
|---------|--------|------------------------------------------------|
| P1 | `WasapiAudioCaptureService` rents callback buffers from `ArrayPool<float>` | 15.4 MB → **0 B** per 10 s of simulated capture; no more Gen2/LOH churn while recording |
| P2 | `AudioPipelineService` pre-sizes the sample buffer (`EnsureCapacity`) + span `AddRange` | 3.8× faster appends, 2.2× less garbage than the per-sample loop (pre-sizing is what removes the growth garbage — plain `AddRange` alone allocated *more*) |
| P3 | Streaming window: single span-slice copy instead of `GetRange().ToArray()` | ½ the allocation, 2.2× faster per window |
| P4 | `ParakeetSpeechRecognizer` passes the utterance array zero-copy (`MemoryMarshal.TryGetArray`) | removes one full-utterance `float[]` duplicate per transcription on the default engine (not micro-benchmarked; code-inspection + tests) |
| P8a | Level event args allocated only when `LevelChanged` has subscribers | removes ~10–20 small allocs/s on the capture thread when the widget is hidden |

## Contract change

`AudioDataEventArgs.Buffer` is now documented as valid only during the event
(pool-backed). The pipeline's synchronous copy is regression-tested by
`AudioPipelineTests.Pipeline_CopiesEventBufferSynchronously_SurvivesSourceMutation`.

## Test status

`dotnet test`: 841 passed / 0 failed (110 benchmark, 434 core/platform,
297 desktop) after all Phase 1 changes.

Live GC-counter measurement (`dotnet-counters` during a real dictation) still
pending — requires an interactive desktop session; tracked in the plan's
verification notes.
