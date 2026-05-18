---
title: Parlotype.Platform
type: service-profile
status: active
tags: [platform, implementations, whisper, nAudio, vad]
criticality: high
last_updated: 2026-05-17
summary: Implements Core interfaces using Whisper.net, NAudio, SileroVad, SharpHook
---

# Parlotype.Platform

## Purpose
Platform-specific implementations of all Core interfaces. Where the real audio capture, VAD, transcription, hotkeys, and settings logic lives.

## Key Paths
- `src/Parlotype.Platform/Audio/` — `WasapiAudioCaptureService`, `SileroVadService`, `MicrophoneEnumerator`, `AudioPipelineService` (also implements `IAudioLevelProvider`)
- `src/Parlotype.Platform/Speech/` — `WhisperSpeechRecognizer`, `LlamaCppSpeechRecognizer` (Gemma 4 via llama-server sidecar; also implements `ILlamaCppServerLifecycle` so the installer can stop it before deleting files), `DelegatingSpeechRecognizer` (routes by `SpeechEngine` setting), `SpeechRecognizerFactory`, `Gemma4ModelDownloadService`, `HttpModelDownloadService` (delegates the download loop to `StreamingFileDownloader`), `StreamingFileDownloader` (shared HTTP → temp → atomic-move helper), `WhisperModelTypeExtensions`, `WhisperRuntimeBootstrap` (CUDA/Vulkan/CPU runtime selection + Whisper.net diagnostics bridge), `WindowsNvidiaEnvironmentProvider`, `NoOpNvidiaEnvironmentProvider`, `WindowsVulkanEnvironmentProvider`, `NoOpVulkanEnvironmentProvider`
- `src/Parlotype.Platform/Speech/LlamaServer/` — managed-install subsystem (ADR-026): `JsonLlamaServerRegistry` (manifest.json + `LlamaCppActiveInstall` selector), `LlamaServerAssetParser` (tolerant filename parser), `GitHubLlamaServerCatalog` (HTTP + ETag + 1h on-disk cache, OS/arch filter at read time), `LlamaServerInstaller` (staging dir + SHA256 verify + ZIP extract + atomic Directory.Move + cudart companion merge; `BuildInstallId` is public so the UI can pair catalog rows with installed entries)
- `src/Parlotype.Platform/Hotkeys/` — `SharpHookHotkeyService`, `KeyCodeMapper`
- `src/Parlotype.Platform/Settings/` — `JsonSettingsService`
- `src/Parlotype.Platform/PlatformServiceExtensions.cs` — DI registration (all singletons)

## Key External Dependencies
- **Whisper.net** + **Whisper.net.Runtime.Cuda** (conditional, ~350 MB) + **Whisper.net.Runtime.Vulkan** (always, ~30 MB) — transcription
- **NAudio** — WASAPI audio capture
- **Microsoft.ML.OnnxRuntime** — Silero VAD ONNX inference
- **SharpHook** — global keyboard hooks

## Conventions
- Register all new services in `PlatformServiceExtensions.cs`
- Mirror Core's subfolder structure
- Runtime selection: `Auto` chains CUDA → Vulkan → CPU silently; `Cuda` and `Vulkan` are strict and throw `RuntimeUnavailableException` when unavailable (no silent CPU fallback)
- NVIDIA + Vulkan env provider DI selection is gated by `OperatingSystem.IsWindows()` (Windows impl vs no-op)
- `WhisperRuntimeBootstrap` bridges Whisper.net's internal log via `LogProvider.AddLogger`; remaps the inverted `WhisperLogLevel` enum (see [[whisper-net-quirks]])

## Dependencies
- [[core]]

## Related Decisions
- [[decisions/_index|ADR-003]] Audio pipeline
- [[decisions/_index|ADR-008]] Incremental VAD
- [[decisions/_index|ADR-012]] CUDA GPU acceleration
- [[decisions/_index|ADR-014]] NVIDIA/CUDA environment detection
- [[decisions/_index|ADR-017]] Whisper model hot-swap via `UnloadAsync`
- [[decisions/_index|ADR-022]] Vulkan GPU acceleration
- [[decisions/_index|ADR-023]] Audio-level provider (RMS computation in `AudioPipelineService`)
- [[decisions/_index|ADR-025]] Gemma 4 via llama.cpp sidecar
- [[decisions/_index|ADR-026]] Managed llama.cpp server installation (catalog + registry + installer; manual folder mode preserved)
