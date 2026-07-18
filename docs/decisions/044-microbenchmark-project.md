---
status: accepted
date: 2026-07-13
---

# 044. Micro-benchmark Project (BenchmarkDotNet)

## Context

The 2026-07 audio-pipeline performance work
(plans/2026-07-11-audio-pipeline-perf-security) needed reproducible
before/after evidence for allocation claims. The existing
`Parlotype.Benchmark` project measures *transcription quality* (WER/CER/RTF)
end-to-end; it is the wrong tool for micro-level questions like "how many
bytes does one WAV encode allocate" — those need BenchmarkDotNet's
`MemoryDiagnoser` and statistical engine.

## Decision

New console project `src/Parlotype.MicroBenchmarks` (net10.0):

- **BenchmarkDotNet 0.14** with `[MemoryDiagnoser]` and `[ShortRunJob]` on
  every suite — allocation columns are the primary signal (deterministic);
  wall-clock is secondary and machine-dependent.
- References Core + Platform; `InternalsVisibleTo` added for the internal
  `WavEncoder`. Deterministic seeded synthetic audio, no model loads, no GPU.
- **Not** a test project: `dotnet test` is unaffected. Run manually with
  `dotnet run -c Release --project src/Parlotype.MicroBenchmarks -- --filter *`.
- Frozen "legacy" copies of rewritten code (e.g. `LegacyWavEncoder`) live in
  the benchmark project so before/after appear in a single run and stay
  comparable after the production code moves on.
- `BenchmarkDotNet.Artifacts/` is git-ignored; curated result tables are
  committed to the owning plan folder instead.

## Consequences

- Performance claims in ADR-045 are reproducible on any machine with one
  command; future hot-path changes can extend the same suites.
- One more project in the solution (compiles in Debug so the zero-warning
  build gate still covers it) and a dev-only dependency on BenchmarkDotNet.
- Naming: "Benchmark" (quality) vs "MicroBenchmarks" (allocations/latency) is
  a deliberate distinction; keep new quality metrics out of MicroBenchmarks.
