---
title: Parlotype.Core
type: service-profile
status: active
tags: [core, contracts, domain]
criticality: high
last_updated: 2026-04-28
summary: Domain interfaces and models — zero external dependencies, all contracts live here
---

# Parlotype.Core

## Purpose
Pure domain layer containing all interfaces, models, enums, and records. Zero external NuGet dependencies. Every other project depends on Core.

## Key Paths
- `src/Parlotype.Core/Audio/` — `IAudioCaptureService`, `IMicrophoneEnumerator`, `IVoiceActivityDetector`
- `src/Parlotype.Core/Speech/` — `ISpeechRecognizer`, `WhisperOptions`, `WhisperModelType`, `WhisperModelInfo`, `INvidiaEnvironmentProvider`, `NvidiaEnvironmentInfo`
- `src/Parlotype.Core/Hotkeys/` — `IGlobalHotkeyService`, `HotkeyBinding`, `HotkeyConflictDetector`
- `src/Parlotype.Core/Settings/` — `ISettingsService`, `SettingsKeys`
- `src/Parlotype.Core/TextInjection/` — `ITextInjectionService`, `ITargetWindowTracker`

## Conventions
- Interfaces only — no implementations
- No platform-specific packages ever
- Subfolder structure mirrors Platform's implementation layout
- New domain contracts go here first, then implement in [[platform]]

## Dependencies
None (by design).

## Related Decisions
- [[decisions/_index|ADR-002]] Solution architecture
- [[decisions/_index|ADR-014]] NVIDIA/CUDA environment detection (provider contract lives here)
- [[decisions/_index|ADR-017]] Whisper model hot-swap (`ISpeechRecognizer.UnloadAsync`)
