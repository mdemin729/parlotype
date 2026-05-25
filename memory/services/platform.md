---
title: Parlotype.Platform
type: service-profile
status: active
tags: [platform, implementations, whisper, nAudio, vad]
criticality: high
last_updated: 2026-05-22
summary: Implements Core interfaces using Whisper.net, NAudio, SileroVad, SharpHook
---

# Parlotype.Platform

## Purpose
Platform-specific implementations of all Core interfaces. Where the real audio capture, VAD, transcription, hotkeys, and settings logic lives.

## Key Paths
- `src/Parlotype.Platform/Audio/` — `WasapiAudioCaptureService`, `SileroVadService`, `MicrophoneEnumerator`, `AudioPipelineService` (also implements `IAudioLevelProvider`)
- `src/Parlotype.Platform/Speech/` — `WhisperSpeechRecognizer`, `LlamaCppSpeechRecognizer` (Gemma 4 via llama-server sidecar; speech consumer of `LlamaServer`; resolves the active `PromptTemplate` per `TranscribeAsync` call and renders `{language}`, no model reload on prompt change; also implements `ILlamaCppServerLifecycle` so the installer can stop it before deleting files), `JsonPromptTemplateRegistry` (`prompts.json` + `ActivePromptId` selector; non-deletable built-in default merged at read time, corrupt-file quarantine; ADR-030), `DelegatingSpeechRecognizer` (routes by `SpeechEngine` setting), `SpeechRecognizerFactory`, `Gemma4ModelDownloadService` (per-model download/delete of the 5-entry GGUF+mmproj catalog with cumulative progress; streams scoped before atomic move, ADR-029), `HttpModelDownloadService` (delegates the download loop to `StreamingFileDownloader`), `StreamingFileDownloader` (shared HTTP → temp → atomic-move helper), `WhisperModelTypeExtensions`, `WhisperRuntimeBootstrap` (CUDA/Vulkan/CPU runtime selection + Whisper.net diagnostics bridge), `WindowsNvidiaEnvironmentProvider`, `NoOpNvidiaEnvironmentProvider`, `WindowsVulkanEnvironmentProvider`, `NoOpVulkanEnvironmentProvider`. **Future cloud / online speech recognizers will land here as additional `ISpeechRecognizer` implementations selected via new `SpeechEngine` values + a new branch in `SpeechRecognizerFactory` — opt-in, BYOK, see [[../../docs/decisions/032-online-speech-providers-positioning|ADR-032]]**
- `src/Parlotype.Platform/LlamaServer/` — managed-install subsystem (ADR-026, namespace flattened in ADR-027 — workload-agnostic, shared by speech today and future post-processing): `JsonLlamaServerRegistry` (manifest.json + `LlamaCppActiveInstall` selector), `LlamaServerAssetParser` (tolerant filename parser), `LlamaServerAssetDescriptor` (parser-internal projection), `GitHubLlamaServerCatalog` (HTTP + ETag + 1h on-disk cache, OS/arch filter at read time), `LlamaServerInstaller` (staging dir + SHA256 verify + ZIP extract + atomic Directory.Move + cudart companion merge; `BuildInstallId` is public so the UI can pair catalog rows with installed entries), `LlamaCppServerInfo` (probe helper: `/health` + `/props` for adopt-vs-spawn decision, relocated here in ADR-027)
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
- [[decisions/_index|ADR-027]] LlamaServer namespace rescope (moved out of `Speech.*` to flat `Parlotype.*.LlamaServer.*` anticipating post-processing consumer)
- [[decisions/_index|ADR-030]] Configurable Gemma 4 prompts (`JsonPromptTemplateRegistry`; recognizer reads active prompt per call)
