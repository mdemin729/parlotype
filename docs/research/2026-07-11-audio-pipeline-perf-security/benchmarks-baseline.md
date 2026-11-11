# Baseline benchmark results (Phase 0)

Run: 2026-07-13, before any optimization. `Current` = production code at
baseline (identical to `Legacy` for WavEncoder, as expected — the rewrite lands
in Phase 1).

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.8737)
AMD Ryzen 7 5800H, .NET 10.0.9, X64 RyuJIT AVX2, Job=ShortRun
dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter * --join
```

| Type | Method | Param | Mean | Allocated |
|------|--------|-------|-----:|----------:|
| SampleBuffering | PerSampleAdd (baseline) | 30 s | 1,448.9 µs | 4,195,088 B |
| SampleBuffering | SpanAddRange | 30 s | 667.9 µs | 6,547,706 B |
| SampleBuffering | PreSizedSpanAddRange | 30 s | **385.3 µs** | **1,920,224 B** |
| StreamingWindow | GetRangeToArray (baseline) | 9 s buffer | 246.7 µs | 1,152,640 B |
| StreamingWindow | SpanSliceCopy | 9 s buffer | **114.7 µs** | **576,184 B** |
| WavEncoder | Legacy (baseline) | 1 s | 133.0 µs | 64,248 B |
| WavEncoder | Legacy (baseline) | 10 s | 1,390.0 µs | 640,316 B |
| WavEncoder | Legacy (baseline) | 30 s | 3,894.5 µs | 1,922,724 B |
| CaptureBuffer | Allocate (baseline) | 19,200 B × 100 | 184.3 µs | 7,682,400 B |
| CaptureBuffer | PoolRentReturn | 19,200 B × 100 | **0.49 µs** | **0 B** |
| CaptureBuffer | Allocate (baseline) | 38,400 B × 100 | 587.7 µs | 15,363,997 B (Gen2 4,761/1k ops) |
| CaptureBuffer | PoolRentReturn | 38,400 B × 100 | **0.48 µs** | **0 B** |

## Reading the baseline

- **P1 confirmed:** at the 38,400-byte callback size (100 ms of 48 kHz stereo
  float32), each simulated 10 s of capture allocates **15.4 MB and triggers
  Gen2 collections** (the arrays are LOH-sized). Pooling eliminates it
  entirely (0 B) and is ~1,200× faster per callback cycle.
- **P2 nuance:** naïve `AddRange(span)` on an unsized list actually allocates
  *more* garbage than the per-sample loop (different growth pattern) while
  being 2.2× faster — **pre-sizing is required** to get both wins
  (2.2× less garbage than the current code, 3.8× faster). Phase 1 therefore
  ships `EnsureCapacity` + `AddRange` together.
- **P3 confirmed:** span-slice window extraction halves both time and
  allocation vs `GetRange().ToArray()`.
- **P5 baseline:** legacy WavEncoder allocates exactly 2× the final WAV size
  (MemoryStream buffer + `ToArray` copy); rewrite target is 1× with a much
  tighter sample loop.

Time columns are ShortRun (3 iterations) — treat allocation columns as the
primary signal per the plan. StreamingWindow times carry a BDN
small-iteration-time warning (allocation numbers unaffected).
