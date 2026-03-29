---
title: Parlotype.Desktop.Tests
type: service-profile
status: active
tags: [tests, avalonia, headless, ui]
criticality: medium
last_updated: 2026-03-28
summary: Avalonia headless UI tests for view/viewmodel integration
---

# Parlotype.Desktop.Tests

## Purpose
Headless UI tests using `Avalonia.Headless.XUnit`. Tests view/viewmodel integration without a running window.

## Key Path
`src/Parlotype.Desktop.Tests/`

## Conventions
- Use `[AvaloniaFact]` instead of `[Fact]`
- Mock services in `Mocks/` folder (`MockMicrophoneEnumerator`, `MockSettingsService`)
- Tests can instantiate views and assert on visual tree

## Run
```bash
dotnet test src/Parlotype.Desktop.Tests
```

## Dependencies
- [[desktop]], [[core]]
- Avalonia.Headless.XUnit, xUnit

## Related Decisions
- [[decisions/_index|ADR-010]] Avalonia headless UI testing
