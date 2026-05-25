# Implementation Plan — Translation Capability per Whisper Model

Source of truth = `WhisperModelInfo.SupportsTranslation` (Core). Enforcement = pipeline gate.
User intent (`TranslateToEnglish` setting) is preserved; only the effective value is gated.

## 1. Core — capability flag
- `src/Parlotype.Core/Speech/WhisperModelInfo.cs`
  - Add `bool SupportsTranslation` to the record.
  - `false`: `TinyEn, BaseEn, SmallEn, MediumEn, LargeV3Turbo`. `true`: the rest.

## 2. Platform — authoritative gate
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` (BuildWhisperOptions)
  - `TranslateToEnglish = translate && WhisperModelInfo.Get(modelType).SupportsTranslation`.

## 3. Desktop — model list hint
- `src/Parlotype.Desktop/ViewModels/WhisperModelDisplayItem.cs` — expose `SupportsTranslation`.
- `src/Parlotype.Desktop/Views/Settings/WhisperModelSettingsView.axaml` — muted "no translation"
  TextBlock, `IsVisible="{Binding !SupportsTranslation}"` (new grid column).

## 4. Desktop — disable toggle, preserve intent
- `src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs`
  - `[ObservableProperty] bool _canTranslate = true`.
  - `InitializeAsync` reads `SelectedWhisperModel` → `UpdateTranslationAvailability`.
  - `public void UpdateTranslationAvailability(WhisperModelType)` sets `CanTranslate`;
    does **not** touch `TranslateToEnglishEnabled`.
- `src/Parlotype.Desktop/Views/Settings/WhisperOutputSettingsView.axaml`
  - `ToggleSwitch IsEnabled="{Binding CanTranslate}"` + warning TextBlock `IsVisible="{Binding !CanTranslate}"`.

## 5. Desktop — wire the two sections
- `src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs`
  - Subscribe to `WhisperModel.PropertyChanged`; on `SelectedModel` change call
    `WhisperOutput.UpdateTranslationAvailability(...)`. Call once in constructor for initial state.
  - Mirrors the existing `OnSpeechEnginePropertyChanged` pattern.

## 6. Tests
- `src/Parlotype.Tests/AudioPipelineTests.cs` — Turbo+intent → effective false; Medium+intent → true.
- `src/Parlotype.Tests/WhisperModelInfoTests.cs` — per-model `SupportsTranslation` values + full coverage.
- `src/Parlotype.Desktop.Tests/WhisperOutputSettingsViewModelTests.cs` — init gating + intent preserved.

## 7. Docs & memory
- ADR `docs/decisions/033-translation-model-capability.md`.
- `memory/decisions/_index.md` (ADR-033 row), `memory/services/core.md`, `memory/services/desktop.md`,
  `memory/knowledge/whisper-translation-models.md` (corrected: Turbo does NOT translate).

## Verification
1. `dotnet build Parlotype.slnx` — zero new warnings.
2. `dotnet test` — green.
3. Manual: model list hints; toggle disables on Turbo with note; preference restored on switching
   back to Medium; pipeline logs `Translate=False` on Turbo even when setting is true.
