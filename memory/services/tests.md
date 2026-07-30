---
title: Parlotype.Tests
type: service-profile
status: active
tags: [tests, xunit, core, platform, llamaserver]
criticality: medium
last_updated: 2026-05-21
summary: xUnit v2 tests for Core and Platform — audio pipeline, VAD, Whisper, LlamaServer subsystem
---

# Parlotype.Tests

## Purpose
Unit and integration tests for Core contracts and Platform implementations.

## Key Path
`src/Parlotype.Tests/` — subfolders include `LlamaServer/` (catalog/parser/installer tests using fixtures under `Fixtures/llama-cpp-releases.json`) and `resources/` (audio fixtures for VAD/Whisper).

## Coverage Areas
- Audio capture & format conversion
- Silero VAD incremental processing
- Whisper.net integration (model loading, transcription, hot-swap via `UnloadAsync`)
- `LlamaServer` subsystem: `LlamaServerAssetParser`, `GitHubLlamaServerCatalog` (HTTP+ETag+cache), `JsonLlamaServerRegistry`, `LlamaServerInstaller` (staging + SHA256 + atomic move + cudart companion)
- Settings persistence (`JsonSettingsService` with `SemaphoreSlim`)
- Hotkey binding parsing and key-code mapping; gesture recognition (`ModifierTapTrackerTests`, `ModifierHoldTrackerTests`, `HotkeyGestureMatcherTests` — pure timestamp-driven, no hook needed), `HotkeyBindingCodecTests`, `HotkeySettingsMigratorTests`, `HotkeyConflictCheckTests`, `DictationHotkeyTests`, `HotkeyHintTests` (ADR-047)

## Run
```bash
dotnet test src/Parlotype.Tests
dotnet test src/Parlotype.Tests -p:EnableCuda=false   # CPU-only (skip CUDA runtime)
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"
```

## Dependencies
- [[core]], [[platform]]
- xUnit 2.9.x (v2)

