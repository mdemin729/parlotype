---
title: Parlotype.Benchmark.Tests
type: service-profile
status: active
tags: [tests, benchmark, metrics, wer]
criticality: medium
last_updated: 2026-03-28
summary: xUnit tests for benchmark metrics, comparison engine, formatters, SQLite index
---

# Parlotype.Benchmark.Tests

## Purpose
Tests for WER/CER calculators, text normalization, config deserialization, comparison engine, CSV/Markdown formatters, SQLite index, sweep expansion, repetition stats, memory metrics, and regression checks.

## Key Path
`src/Parlotype.Benchmark.Tests/`

## Run
```bash
dotnet test src/Parlotype.Benchmark.Tests
```

## Dependencies
- [[benchmark]], [[core]]
- xUnit
