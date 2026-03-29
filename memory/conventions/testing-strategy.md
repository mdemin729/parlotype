---
title: Testing Strategy
type: convention
status: active
tags: [testing, xunit, headless, benchmark]
last_updated: 2026-03-28
summary: xUnit for unit tests, Avalonia headless for UI tests, benchmark tests for metrics
---

# Testing Strategy

## Test Projects

| Project | Framework | What it tests |
|---------|-----------|--------------|
| `Parlotype.Tests` | xUnit | Core contracts, Platform implementations (audio, VAD, Whisper) |
| `Parlotype.Desktop.Tests` | Avalonia.Headless.XUnit | View/viewmodel integration, UI behavior |
| `Parlotype.Benchmark.Tests` | xUnit | Metrics, comparison engine, formatters, SQLite, sweep, regression |

## Headless UI Testing
- Use `[AvaloniaFact]` instead of `[Fact]`
- Mock services live in `Mocks/` folder
- Key mocks: `MockMicrophoneEnumerator`, `MockSettingsService`
- Can instantiate views and assert on visual tree

## Writing Tests
- Always write tests for logic in Core and Platform
- Benchmark tests cover: WER/CER calculators, text normalization, config deserialization, comparison engine, CSV/Markdown formatters, SQLite index, sweep expansion, repetition stats, memory metrics, regression checks

## Running Tests
```bash
dotnet test                                      # All tests
dotnet test src/Parlotype.Tests                  # Core + Platform only
dotnet test src/Parlotype.Desktop.Tests          # UI only
dotnet test src/Parlotype.Benchmark.Tests        # Benchmark only
dotnet test -p:EnableCuda=false                  # CPU-only (skip CUDA)
dotnet test --filter "FullyQualifiedName~Name"   # Single test
```
