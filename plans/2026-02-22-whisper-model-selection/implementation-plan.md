---
title: Whisper model selection
status: completed
created: 2026-02-22
started: 2026-02-22
completed: 2026-02-22
---

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

---

# Plan: Model Download Confirmation Dialog with Progress

## Problem
When a Whisper model is not cached locally, `WhisperSpeechRecognizer.EnsureModelAsync` silently downloads it (75 MiB – 2.9 GiB). The user gets no confirmation prompt, no progress feedback, and no way to cancel. We need a modal dialog that:
1. Asks confirmation before downloading (showing model name + size)
2. Shows a progress bar during download
3. Allows cancellation via a Cancel button
4. Blocks other UI interaction while downloading (modal)

## Approach

### Architecture
Introduce a **Core-level service interface** `IModelDownloadService` that abstracts model downloading with progress reporting. Platform implements it using `HttpClient` (replacing `WhisperGgmlDownloader`). Desktop provides a modal dialog window that drives the interaction.

The flow when a model is needed but not cached:
1. `WhisperSpeechRecognizer.EnsureModelAsync` checks if model file exists → if not, it calls `IModelDownloadService.EnsureModelAsync(...)` instead of `WhisperGgmlDownloader`
2. The Desktop-layer implementation of `IModelDownloadService` shows a confirmation dialog → if user confirms, starts download with progress → if user cancels, throws `OperationCanceledException`
3. The Platform-layer provides an `HttpModelDownloadService` that does the actual HTTP download with `IProgress<double>` reporting — Desktop wraps this with UI

**Key design decision:** Split responsibilities:
- `IModelDownloadService` (Core) — interface with `Task<string> EnsureModelAsync(WhisperModelType, CancellationToken)`
- `HttpModelDownloadService` (Platform) — HTTP download with progress, no UI knowledge
- `ModelDownloadDialogService` (Desktop) — wraps `HttpModelDownloadService`, shows modal dialog

This way Platform remains UI-free, and Desktop handles the dialog.

### Download URLs
Whisper.net's `WhisperGgmlDownloader` downloads from Hugging Face. We'll use the same URLs by extracting them from the library's source. The URL pattern is:
`https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{model}.bin`

## Todos

### 1. `core-download-iface` — Add IModelDownloadService to Core
- File: `src/Parlotype.Core/Speech/IModelDownloadService.cs`
- Interface with:
    - `Task<string> EnsureModelAsync(WhisperModelType type, CancellationToken ct)` — returns path to cached model, may prompt user
    - `bool IsModelCached(WhisperModelType type)` — checks if model file exists locally

### 2. `core-download-progress` — Add ModelDownloadProgress record to Core
- File: `src/Parlotype.Core/Speech/ModelDownloadProgress.cs`
- Record: `long BytesReceived`, `long? TotalBytes`, `double? ProgressFraction`

### 3. `platform-http-download` — Implement HttpModelDownloadService in Platform
- File: `src/Parlotype.Platform/Speech/HttpModelDownloadService.cs`
- Downloads from Hugging Face GGML CDN using `HttpClient` with streaming
- Reports progress via `IProgress<ModelDownloadProgress>`
- Writes to temp file, moves on completion
- Uses model cache directory: `%LOCALAPPDATA%/parlotype/models/`
- Public method: `Task DownloadModelAsync(WhisperModelType, IProgress<ModelDownloadProgress>, CancellationToken)`
- Public method: `string GetModelPath(WhisperModelType)` — returns expected file path
- Public method: `bool IsModelCached(WhisperModelType)` — checks if file exists

### 4. `platform-recognizer-update` — Update WhisperSpeechRecognizer to use IModelDownloadService
- File: `src/Parlotype.Platform/Speech/WhisperSpeechRecognizer.cs`
- Replace internal `EnsureModelAsync` with call to `IModelDownloadService.EnsureModelAsync`
- Remove `WhisperGgmlDownloader` usage entirely
- Remove `ModelDownloadLock` (download service handles this)

### 5. `desktop-download-vm` — Add ModelDownloadViewModel
- File: `src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs`
- Properties: `string ModelName`, `string ModelSize`, `double ProgressValue` (0–100), `string StatusText`, `bool IsDownloading`, `bool IsConfirming`
- Commands: `DownloadCommand`, `CancelCommand`
- States: Confirmation → Downloading → Done / Cancelled

### 6. `desktop-download-dialog` — Add ModelDownloadDialog window
- File: `src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml` + `.axaml.cs`
- Modal `Window` with:
    - Confirmation state: "Download {ModelName} ({Size})?" with Download/Cancel buttons
    - Downloading state: progress bar + percentage text + Cancel button
    - Styled consistently with existing app (Fluent theme, same border/corner radius patterns)
- `ShowAsync(Window owner)` returns `bool` (true = downloaded, false = cancelled)

### 7. `desktop-download-service` — Implement ModelDownloadDialogService in Desktop
- File: `src/Parlotype.Desktop/Services/ModelDownloadDialogService.cs`
- Implements `IModelDownloadService` from Core
- `EnsureModelAsync`: checks if cached → if not, shows dialog on UI thread → calls `HttpModelDownloadService` → returns path
- `IsModelCached`: delegates to `HttpModelDownloadService`
- Needs reference to main window (for modal owner) — passed via constructor or service locator

### 8. `desktop-di-update` — Update DI registration
- File: `src/Parlotype.Platform/PlatformServiceExtensions.cs` — register `HttpModelDownloadService`
- File: `src/Parlotype.Desktop/App.axaml.cs` — register `ModelDownloadDialogService` as `IModelDownloadService`, pass window reference

### 9. `tests-update-download` — Update tests
- `src/Parlotype.Tests/WhisperSpeechRecognizerTests.cs` — provide mock `IModelDownloadService`
- `src/Parlotype.Tests/AudioPipelineTests.cs` — same
- Optionally: unit test `HttpModelDownloadService.IsModelCached`

### 10. `docs-update-download` — Update documentation
- `AGENTS.md` — document the download service pattern and dialog

## Notes
- The dialog must show on the UI thread. `ModelDownloadDialogService.EnsureModelAsync` dispatches to `Dispatcher.UIThread` when needed.
- `WhisperGgmlDownloader` from Whisper.net is removed entirely — we use direct HTTP download for control over progress and cancellation.
- The confirmation dialog shows the model's `DiskSize` from `WhisperModelInfo` (user-facing), not the raw byte count.
- If the user cancels, `OperationCanceledException` propagates up, and the caller (pipeline start) should handle it gracefully.
- The modal blocks only the main window — system tray or background services (if any) are unaffected.
