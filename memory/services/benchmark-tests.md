---
title: Parlotype.Benchmark.Tests
type: service-profile
status: active
tags: [tests, benchmark, metrics, wer, gemma4]
criticality: medium
last_updated: 2026-05-21
summary: xUnit v2 tests for benchmark metrics, comparison engine, formatters, SQLite index, sweep, repetitions, regression
---

# Parlotype.Benchmark.Tests

## Purpose
Tests for WER/CER calculators, text normalization, config deserialization, comparison engine, CSV/Markdown/JSON formatters, SQLite index, sweep expansion, repetition stats (mean/stddev), per-sample memory/GC metrics, and CI regression checks.

## Key Path
`src/Parlotype.Benchmark.Tests/`

## Run
```bash
dotnet test src/Parlotype.Benchmark.Tests
```

## Dependencies
- [[benchmark]], [[core]], `Parlotype.Gemma4`
- xUnit 2.9.x (v2)

