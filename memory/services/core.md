---
title: Parlotype.Core
type: service-profile
status: active
tags: [core, contracts, domain]
criticality: high
last_updated: 2026-05-22
summary: Domain interfaces and models — zero external dependencies, all contracts live here
---

# Parlotype.Core

## Purpose
Pure domain layer containing all interfaces, models, enums, and records. Zero external NuGet dependencies. Every other project depends on Core.

## Key Paths
- `src/Parlotype.Core/Audio/` — `IAudioCaptureService`, `IMicrophoneEnumerator`, `IVoiceActivityDetector`, `IAudioLevelProvider`, `RecordingState`
- `src/Parlotype.Core/Speech/` — `ISpeechRecognizer`, `WhisperOptions`, `WhisperModelType`, `WhisperModelInfo`, `SpeechEngine` (Whisper/Gemma4 today; cloud / online provider values planned per [[decisions/_index|ADR-032]] — local by default, cloud opt-in), `Gemma4ModelInfo`, `IPromptTemplateRegistry`, `PromptTemplate` (with `{language}` token + `Render`), `RuntimePreference` (Auto/Cuda/Vulkan/Cpu), `RuntimeUnavailableException`, `INvidiaEnvironmentProvider`, `NvidiaEnvironmentInfo`, `IVulkanEnvironmentProvider`, `VulkanEnvironmentInfo`, `VulkanDeviceInfo`
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
- [[decisions/_index|ADR-022]] Vulkan GPU acceleration (`IVulkanEnvironmentProvider`, `RuntimeUnavailableException`, extended `RuntimePreference`)
- [[decisions/_index|ADR-023]] Audio-level provider & waveform visualisation (`IAudioLevelProvider`, `RecordingState`)
- [[decisions/_index|ADR-030]] Configurable Gemma 4 prompts (`IPromptTemplateRegistry`, `PromptTemplate`, `SettingsKeys.ActivePromptId`)
