---
title: Parlotype.Core
type: service-profile
status: active
tags: [core, contracts, domain]
criticality: high
last_updated: 2026-06-11
summary: Domain interfaces and models — zero external dependencies, all contracts live here
---

# Parlotype.Core

## Purpose
Pure domain layer containing all interfaces, models, enums, and records. Zero external NuGet dependencies. Every other project depends on Core.

## Key Paths
- `src/Parlotype.Core/Audio/` — `IAudioCaptureService`, `IMicrophoneEnumerator`, `IVoiceActivityDetector`, `IAudioLevelProvider`, `RecordingState`
- `src/Parlotype.Core/Speech/` — `ISpeechRecognizer`, `WhisperOptions`, `WhisperModelType`, `WhisperModelInfo` (with `SupportsTranslation` flag — false for `*En` models + `LargeV3Turbo`, see [[decisions/_index|ADR-033]]), `SpeechEngine` (Whisper/Gemma4 today; cloud / online provider values planned per [[decisions/_index|ADR-032]] — local by default, cloud opt-in), `Gemma4ModelInfo`, `IPromptTemplateRegistry`, `PromptTemplate` (with `{language}` token + `Render`), `LanguageInfo` + `LanguageCatalog` (curated `WhisperLanguages` ~99 + `CultureInfo`-derived `AllLanguages` fallback; `AutoDetectCode`/`NoTranslationCode`/`KeyboardLayoutCode` sentinels + `IsKeyboardLayout`, ADR-036), `LanguageCapabilities` + `SpeechEngineCapabilities.For` (per-engine source set + `FixedTranslationTargets` — Whisper publishes `[English]`, ADR-035 — and derived `TranslationForm` None/Toggle/Full, ADR-036), `TranslationForm` enum, `IKeyboardLayoutService` + `KeyboardLayoutInfo` (OS keyboard-layout language detection contract, ADR-036), `SourceLanguageResolver` (pure keyboard-sentinel → detected-language resolution with auto fallback, ADR-036), `RecentLanguages` (pure MRU helper, cap 5, role-agnostic — ignores all three sentinels), `LanguageSettingsMigrator` (idempotent one-shot migration from legacy `TranslateToEnglish` and shared `RecentLanguages` to `TranslationEnabled` + per-role MRUs, ADR-035) — all [[decisions/_index|ADR-034]] / [[decisions/_index|ADR-035]] / [[decisions/_index|ADR-036]], `RuntimePreference` (Auto/Cuda/Vulkan/Cpu), `RuntimeUnavailableException`, `INvidiaEnvironmentProvider`, `NvidiaEnvironmentInfo`, `IVulkanEnvironmentProvider`, `VulkanEnvironmentInfo`, `VulkanDeviceInfo`
- `src/Parlotype.Core/Settings/` keys for language: `SelectedSourceLanguage`, `SelectedTargetLanguage`, `TranslationEnabled` (master toggle, ADR-035), per-role MRUs `RecentSourceLanguages` / `RecentTargetLanguages` (ADR-035). Legacy `TranslateToEnglish` and shared `RecentLanguages` keys retained for one-shot migration only
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
- [[decisions/_index|ADR-034]] Source & target language selection (`LanguageCatalog`, `LanguageCapabilities`, `RecentLanguages`)
- [[decisions/_index|ADR-035]] Language settings UX redesign (`LanguageSettingsMigrator`, `TranslationEnabled`, per-role MRU keys, Whisper `FixedTranslationTargets` populated)
- [[decisions/_index|ADR-036]] Language UX rebuild (`KeyboardLayoutCode` sentinel, `TranslationForm`, `IKeyboardLayoutService`/`KeyboardLayoutInfo`, `SourceLanguageResolver`)
