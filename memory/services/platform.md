---
title: Parlotype.Platform
type: service-profile
status: active
tags: [platform, implementations, whisper, nAudio, vad]
criticality: high
last_updated: 2026-04-28
summary: Implements Core interfaces using Whisper.net, NAudio, SileroVad, SharpHook
---

# Parlotype.Platform

## Purpose
Platform-specific implementations of all Core interfaces. Where the real audio capture, VAD, transcription, hotkeys, and settings logic lives.

## Key Paths
- `src/Parlotype.Platform/Audio/` — `WasapiAudioCaptureService`, `SileroVadService`, `MicrophoneEnumerator`
- `src/Parlotype.Platform/Speech/` — `WhisperSpeechRecognizer`, `WhisperModelTypeExtensions`, `WhisperRuntimeBootstrap` (CUDA/CPU runtime selection + Whisper.net diagnostics bridge), `WindowsNvidiaEnvironmentProvider`, `NoOpNvidiaEnvironmentProvider`
- `src/Parlotype.Platform/Hotkeys/` — `SharpHookHotkeyService`, `KeyCodeMapper`
- `src/Parlotype.Platform/Settings/` — `JsonSettingsService`
- `src/Parlotype.Platform/PlatformServiceExtensions.cs` — DI registration (all singletons)

## Key External Dependencies
- **Whisper.net** + **Whisper.net.Runtime.Cuda** — transcription (CUDA optional, ~350 MB)
- **NAudio** — WASAPI audio capture
- **Microsoft.ML.OnnxRuntime** — Silero VAD ONNX inference
- **SharpHook** — global keyboard hooks

## Conventions
- Register all new services in `PlatformServiceExtensions.cs`
- Mirror Core's subfolder structure
- CUDA auto-detected via `RuntimeOptions.RuntimeLibraryOrder`, CPU fallback silent
- NVIDIA env provider DI selection is gated by `OperatingSystem.IsWindows()` (Windows impl vs no-op)
- `WhisperRuntimeBootstrap` bridges Whisper.net's internal log via `LogProvider.AddLogger`; remaps the inverted `WhisperLogLevel` enum (see [[whisper-net-quirks]])

## Dependencies
- [[core]]

## Related Decisions
- [[decisions/_index|ADR-003]] Audio pipeline
- [[decisions/_index|ADR-008]] Incremental VAD
- [[decisions/_index|ADR-012]] CUDA GPU acceleration
- [[decisions/_index|ADR-014]] NVIDIA/CUDA environment detection
