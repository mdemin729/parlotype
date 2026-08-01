---
title: Testing Strategy
type: convention
status: active
tags: [testing, xunit, xunitv3, headless, benchmark, screenshots]
last_updated: 2026-05-21
summary: xUnit v2 for Core/Platform/Benchmark; xUnit v3 for Desktop headless UI; Skia required for screenshot tests
---

# Testing Strategy

## Test Projects

| Project | Framework | What it tests |
|---------|-----------|--------------|
| `Parlotype.Tests` | xUnit **v2** (2.9.x) | Core contracts, Platform implementations — audio, VAD, Whisper, `LlamaServer` parser/installer/catalog |
| `Parlotype.Desktop.Tests` | **xUnit v3** (3.x) + `Avalonia.Headless.XUnit` 12.x | View/viewmodel integration, UI behavior, screenshot tests |
| `Parlotype.Benchmark.Tests` | xUnit **v2** (2.9.x) | Metrics, comparison engine, formatters, SQLite, sweep, regression — also references `Parlotype.Gemma4` |

> xUnit versions differ on purpose: `Avalonia.Headless.XUnit` 12.x **requires xUnit v3**, while the other two test projects stayed on v2 (no migration trigger). Tests that touch `Dispatcher.UIThread` must use `[AvaloniaFact]`, not `[Fact]`, or they hang.

## Headless UI Testing
- Use `[AvaloniaFact]` instead of `[Fact]`
- Mock services live in `Mocks/` folder
- Key mocks: `MockMicrophoneEnumerator`, `MockSettingsService`
- Can instantiate views and assert on visual tree
- **Screenshot tests** (`CaptureRenderedFrame`) require **`Avalonia.Skia` + `UseSkia()` and `UseHeadlessDrawing = false`** in `TestAppBuilder`. The default headless platform uses a fake drawing backend that produces no pixels.

## Writing Tests
- Always write tests for logic in Core and Platform
- Benchmark tests cover: WER/CER calculators, text normalization, config deserialization, comparison engine, CSV/Markdown formatters, SQLite index, sweep expansion, repetition stats, memory metrics, regression checks
- Headless model download in UI tests uses `HeadlessModelDownloadService` (no dialog)

## Running Tests
```bash
dotnet test                                      # All tests
dotnet test src/Parlotype.Tests                  # Core + Platform only
dotnet test src/Parlotype.Desktop.Tests          # UI only
dotnet test src/Parlotype.Benchmark.Tests        # Benchmark only
dotnet test --filter "FullyQualifiedName~Name"   # Single test
```

