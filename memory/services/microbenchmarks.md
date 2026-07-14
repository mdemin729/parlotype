---
title: Parlotype.MicroBenchmarks
type: service-profile
status: active
tags: [benchmarks, benchmarkdotnet, performance, allocations]
criticality: low
last_updated: 2026-07-13
summary: BenchmarkDotNet micro-benchmarks for audio-pipeline hot paths (allocations first, time second)
---

# Parlotype.MicroBenchmarks

## Purpose
Reproducible allocation/latency evidence for hot-path changes (ADR-044).
Distinct from [[benchmark]], which measures transcription *quality*
(WER/CER/RTF) end-to-end.

## Usage
```bash
dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter *
```
Not part of `dotnet test`. `BenchmarkDotNet.Artifacts/` is git-ignored;
curated result tables are committed to the owning plan folder
(e.g. `plans/2026-07-11-audio-pipeline-perf-security/benchmarks-*.md`).

## Key Paths
- `WavEncoderBenchmarks` — production `WavEncoder` (via `InternalsVisibleTo`) vs `LegacyWavEncoder` (frozen pre-rewrite copy — do not "fix" it)
- `SampleBufferingBenchmarks`, `StreamingWindowBenchmarks`, `CaptureBufferBenchmarks` — variant comparisons behind the ADR-045 changes
- `SyntheticAudio` — deterministic seeded audio, no model loads, no GPU

## Conventions
- `[MemoryDiagnoser]` + `[ShortRunJob]` on every suite; treat the Allocated
  column as the primary signal (deterministic), time as secondary
- Frozen "legacy" copies of rewritten production code live here so
  before/after stay comparable in a single run

## Dependencies
- [[core]], [[platform]], BenchmarkDotNet 0.14

## Related Decisions
- [[decisions/_index|ADR-044]] Micro-benchmark project
- [[decisions/_index|ADR-045]] Audio pipeline allocation & threading rework (the measurements)
