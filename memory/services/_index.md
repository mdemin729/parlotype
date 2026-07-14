---
title: Service Registry
type: index
status: active
last_updated: 2026-07-13
summary: Registry of all projects in the Parlotype solution
---

# Service Registry

| Project | Type | Purpose | Key Path |
|---------|------|---------|----------|
| [[core]] | Library | Domain interfaces & models | `src/Parlotype.Core/` |
| [[platform]] | Library | Platform implementations | `src/Parlotype.Platform/` |
| [[desktop]] | Application | Avalonia 12 tray-based desktop UI | `src/Parlotype.Desktop/` |
| [[benchmark]] | Application | Transcription quality CLI | `src/Parlotype.Benchmark/` |
| [[microbenchmarks]] | Application | BenchmarkDotNet allocation/latency micro-benchmarks (ADR-044) | `src/Parlotype.MicroBenchmarks/` |
| [[tests]] | Tests | Core + Platform unit tests | `src/Parlotype.Tests/` |
| [[desktop-tests]] | Tests | Avalonia 12 headless UI tests (xUnit v3) | `src/Parlotype.Desktop.Tests/` |
| [[benchmark-tests]] | Tests | Benchmark metric tests | `src/Parlotype.Benchmark.Tests/` |
