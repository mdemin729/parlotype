# Plan: Whisper Model Selection in Settings

## Problem
The Whisper model type is currently hardcoded (`GgmlType.Base` default) in `WhisperSpeechRecognizer`'s constructor. Users cannot choose which model to use. We need a settings UI to select from all available GGML model types, persist the choice, and wire it through to the speech recognizer.

## Approach
Follow the existing settings patterns (WaitTime picker, Theme picker, Microphone picker) to add a model selector flyout. Define a Core-level enum to avoid leaking `Whisper.net.Ggml.GgmlType` into Core. Map it to `GgmlType` in Platform. Show model name + disk size in the picker UI.

## Todos

### 1. `core-enum` — Add `WhisperModelType` enum to Core
- File: `src/Parlotype.Core/Speech/WhisperModelType.cs`
- Define enum values mirroring `GgmlType`: Tiny, TinyEn, Base, BaseEn, Small, SmallEn, Medium, MediumEn, LargeV1, LargeV2, LargeV3, LargeV3Turbo
- Keep Core free of Whisper.net dependency

### 2. `core-model-info` — Add `WhisperModelInfo` record to Core
- File: `src/Parlotype.Core/Speech/WhisperModelInfo.cs`
- Record with: `WhisperModelType Type`, `string DisplayName`, `string DiskSize`, `string Sha`
- Static method `GetAll()` returning all model metadata from the user-provided table
- Static method `Get(WhisperModelType)` for lookup

### 3. `core-settings-key` — Add settings key for selected model
- File: `src/Parlotype.Core/Settings/SettingsKeys.cs`
- Add `public const string SelectedWhisperModel = "SelectedWhisperModel";`

### 4. `platform-mapping` — Map `WhisperModelType` → `GgmlType` in Platform
- File: `src/Parlotype.Platform/Speech/WhisperModelTypeExtensions.cs`
- Extension method `ToGgmlType()` on `WhisperModelType`

### 5. `platform-recognizer` — Update `WhisperSpeechRecognizer` to read model from settings
- File: `src/Parlotype.Platform/Speech/WhisperSpeechRecognizer.cs`
- Add `ISettingsService` dependency to constructor
- Remove `GgmlType` constructor parameter
- In `InitializeAsync`, read `SettingsKeys.SelectedWhisperModel` from settings
- Parse to `WhisperModelType`, default to `Base` if missing
- Map to `GgmlType` using the extension method
- Update `EnsureModelAsync` accordingly

### 6. `platform-di` — Update DI registration
- File: `src/Parlotype.Platform/PlatformServiceExtensions.cs`
- No changes expected (constructor injection handles it), but verify after recognizer changes

### 7. `desktop-display-item` — Add `WhisperModelDisplayItem` wrapper
- File: `src/Parlotype.Desktop/ViewModels/WhisperModelDisplayItem.cs`
- Follow `WaitTimeDisplayItem` / `ThemeDisplayItem` pattern
- Properties: `WhisperModelType Type`, `string DisplayName`, `string DiskSize`, `ICommand SelectCommand`
- Embed the select command directly (flyout binding pattern)

### 8. `desktop-viewmodel` — Add model selection to `SettingsViewModel`
- File: `src/Parlotype.Desktop/ViewModels/SettingsViewModel.cs`
- Add `WhisperModelDisplayItem[] ModelOptions` property (populated from `WhisperModelInfo.GetAll()`)
- Add `[ObservableProperty] WhisperModelType _selectedWhisperModel` with default `Base`
- Add `SelectWhisperModel` RelayCommand
- Load saved model in `InitializeAsync()`
- Persist on selection change via `ISettingsService`

### 9. `desktop-view` — Add model picker flyout to settings AXAML
- File: `src/Parlotype.Desktop/Views/SettingsFlyoutView.axaml`
- Add "Whisper model" row between microphone and theme sections
- Flyout with ItemsControl listing `ModelOptions`
- Each item shows display name + disk size (like WaitTime shows name + seconds)
- Use brain/AI icon for the row

### 10. `tests-update` — Update tests for new constructor signatures
- File: `src/Parlotype.Tests/WhisperSpeechRecognizerTests.cs`
- Update constructor calls to remove `GgmlType` parameter, provide mock `ISettingsService`
- File: `src/Parlotype.Desktop.Tests/` — add test for model selection if relevant

### 11. `docs-update` — Update project documentation
- File: `README.md` — mention model selection in features
- File: `AGENTS.md` — note model selection settings pattern if needed

## Notes
- `WhisperModelType` lives in Core to keep the domain boundary clean; `GgmlType` stays internal to Platform
- The model is read from settings at `InitializeAsync` time. Changing the model in settings while recording is active will take effect on next pipeline restart
- Model disk sizes from user-provided table are static metadata; no runtime size detection needed
- SHA hashes stored for future integrity verification (custom downloader feature)
