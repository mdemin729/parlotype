---
title: Parlotype.Desktop
type: service-profile
status: active
tags: [desktop, avalonia, avalonia12, tray, ui, mvvm]
criticality: medium
last_updated: 2026-07-07
summary: Avalonia 12 tray-based desktop frontend — sole desktop app
---

# Parlotype.Desktop

## Purpose

Tray-first desktop frontend on Avalonia 12.0.2 (GA). Sole desktop app after V1 sunset (ADR-018). Reuses `Parlotype.Core` + `Parlotype.Platform`. See ADR [[015-parlotype-desktop-avalonia12]].

## Key Paths

- `src/Parlotype.Desktop/App.axaml(.cs)` — TrayIcon + NativeMenu, DI bootstrap, `ShutdownMode.OnExplicitShutdown`
- `src/Parlotype.Desktop/Services/IWindowManager.cs` + `WindowManager.cs` — single-instance Transcribe + Settings windows; `Closing` handler hides instead of closes; `ShowTranscribe(activate: false)` shows without stealing focus (used by hotkey path); calls `TranscribeWindow.RestorePositionAsync(IWindowStateService)` before the first show so the frameless widget reopens where the user left it (ADR-040)
- `src/Parlotype.Desktop/Services/HotkeyCoordinator.cs` — bridges `IGlobalHotkeyService` → `ShowTranscribe()` + `StartRecordingAsync()`
- `src/Parlotype.Desktop/Services/SilentModelDownloadService.cs` — `IModelDownloadService` impl that downloads Whisper models silently in the background (no dialog; tray app has no always-visible main window)
- `src/Parlotype.Desktop/Services/LlamaServerInstallDialogService.cs` — `ILlamaServerInstaller` wrapper that opens the generalized `ModelDownloadDialog` with phase-aware status text (downloading / downloading-companion / verifying / extracting / finalizing). Registered after `AddPlatformServices()` so its `AddSingleton<ILlamaServerInstaller, ...>` wins via last-registration semantics; the concrete `LlamaServerInstaller` is still resolvable for Benchmark / headless paths (ADR-026)
- `src/Parlotype.Desktop/ViewModels/AppViewModel.cs` — tray menu commands (Open / Settings / Exit), bound from `Application.DataContext`
- `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs` — recording state machine (`RecordingState`, `AudioLevel`, `IsIdle`, `IsActive`, `IsLoading` — ADR-038), EMA-smoothed RMS with 1200ms hold-off for stable Active/Idle transitions. `StartRecordingAsync` shows `RecordingState.Loading` only when the model load outlasts `LoadingSpinnerDelay` (default 200ms, races `StartAsync` vs `Task.Delay`) so a hot model doesn't flash the spinner — ADR-038; `PrewarmAsync` warms the model silently in the background (kicked off from `App` only when the opt-in `SettingsKeys.PrewarmModelOnStartup` flag is true, default false — ADR-038). Also hosts the Phase-2 language surface (ADR-036): consumes the shared `LanguageRelationshipViewModel` (optional ctor param — strip hidden when absent), exposes `SourceShort`/`TargetShort` chips (target mirrors source while translation is off), `ToggleTranslationCommand`, `IsLanguageFlyoutOpen` + `OpenLanguageFlyoutCommand`, an embedded `TargetPicker`, `GoToLanguageSettingsCommand`, and stops recording on `RelationshipChanged` (changes from either surface)
- `src/Parlotype.Desktop/ViewModels/LanguageRelationshipViewModel.cs` — **shared source→target relationship** (ADR-036, DI singleton): engine `Capabilities` + `TargetForm`, source state (keyboard/auto/explicit), target `{on, code}` with resting-target restore, per-role MRU, persistence, `DetectedKeyboardLayout`, derived `Connector`/`ConnectorGlyph`/form + connector booleans/`SummaryText`/`TranslationSwitch`/`ToggleSwitchLabel`/`UnavailableNote`, ADR-033 paused note, and spec-§8 engine-switch fallbacks with auto-clearing toasts (`ToastMessage`). Both the Language page and the Transcribe window delegate to it. **Live keyboard-layout polling**: `BeginLivePolling()`/`EndLivePolling()` (reference-counted) drive a shared ~500ms `DispatcherTimer` that re-runs `RefreshKeyboardLayout()` so the displayed "Detected: …" hint / strip track Alt+Shift layout changes; the timer only ticks while a surface is visible **and** `IsKeyboardLayout(SourceCode)` (re-evaluated via `OnSourceCodeChanged`), exposed by `IsLayoutPollActive`. Surfaces register on visibility: `LanguageSelectionSettingsView` (attach/detach) and `TranscribeWindow` (Opened/Closed). Rationale: a background tray app gets no event for foreground-app layout changes, so the display is kept current by polling (the recording pipeline already re-detects fresh at start)
- `src/Parlotype.Desktop/Views/TranscribeWindow.axaml(.cs)` — **frameless compact widget** (ADR-040): 172×118, `WindowDecorations="None"` + transparent window with a rounded `RootChrome` border; drag only via the top grip strip (`GripZone` → `BeginMoveDrag`, position saved when the Windows modal move loop returns); ✕ button and `Esc` hide to tray; `StatusText` surfaces only as the root tooltip (no visible status line); `RestorePositionAsync`/`SavePositionAsync` persist a single `WindowStateKeys.TranscribeWindowPosition` (`WindowPosition` struct) via `IWindowStateService` — its own `window-state.json`, never `settings.json` — with an off-screen → CenterScreen fallback via `Screens.All`. Chrome behaviour covered by `TranscribeWindowChromeTests`
- `src/Parlotype.Desktop/Views/WaveformView.cs` — custom `Control` rendering four states: mic icon (Disabled), rotating arc spinner (Loading — ADR-038), breathing bars (Idle), animated multi-frequency wave (Active); 60fps `DispatcherTimer`; white bars on blue button background
- `src/Parlotype.Desktop/ViewModels/Settings/` — `SpeechEngineSettingsViewModel` (Whisper/Gemma4/Parakeet cards with `IsGemma4Selected`/`IsParakeetSelected`; also hosts the opt-in `PreloadModelOnStartupEnabled` toggle → `SettingsKeys.PrewarmModelOnStartup`, default false, effect on next launch — ADR-038), `MicrophoneSettingsViewModel`, `SilenceTimeoutSettingsViewModel` (wait time, engine-agnostic — Audio category), `WhisperModelSettingsViewModel` (coordinates model hot-swap: stops recording + unloads model; model list shows a "no translation" hint per `SupportsTranslation`), `RuntimeSettingsViewModel` (CUDA/Vulkan/CPU pinning for Whisper), `WhisperOutputSettingsViewModel` (punctuation, profanity — Whisper-restricted; translation moved to Language page in ADR-035), `LanguageSelectionSettingsViewModel` (thin wrapper over `LanguageRelationshipViewModel`, ADR-036: two popover picker VMs with specials — keyboard+auto on source, "Off — no translation" on target — tile/sub-hint strings, `UpdateForEngine`/`UpdateTranslationAvailability` hooks called by `SettingsWindowViewModel`; the page renders the three-column source|connector|target layout with model-driven forms ToggleSwitch/picker/disabled+amber, summary line, and toast region), `Gemma4ModelSettingsViewModel` (5-model catalog pick keyed by `ModelId` + per-model Download/Delete, SpeechEngine category, Gemma4-restricted, ADR-029), `ParakeetModelSettingsViewModel` (Parakeet catalog pick + Download/Delete via `ParakeetModelDownloadDialogService`, SpeechEngine category, Parakeet-restricted, ADR-041), `PromptSettingsViewModel` (create/edit/duplicate/delete/select transcription prompts via `IPromptTemplateRegistry`; inline `IsEditing` form; built-ins non-editable; SpeechEngine category, Gemma4-restricted, ADR-030), `HotkeySettingsViewModel`, `ThemeSettingsViewModel`, `LlamaCppSettingsViewModel` (managed install browse/install/uninstall/set-active + manual folder picker, ADR-026), `LlamaServerInstallRowVm` / `LlamaServerVariantRowVm` / `LlamaServerBackendFormatter` (row VMs + display helpers for the llama settings page)
- `src/Parlotype.Desktop/ViewModels/LanguagePickerViewModel.cs` + `LanguageRowFactory.cs` + `LanguageDisplayItem.cs` + `Views/Settings/LanguagePickerView.axaml` — reusable popover picker (ADR-036): conditional search box (>8 entries), pinned specials with icons/sub-hints, `Recent`/`All languages` group headers, rich rows (code tile / native subname / check), empty state naming the query, Escape + auto-focus in code-behind. Callback-driven (`getSupported`/`getRecents`/`getSelectedCode`/`onSelect`/`getSpecials`) so the parent owns persistence. Chrome lives in the shared `Border.popoverChrome` app style — page popups host at 300px, the Transcribe flyout at 268px. Used by the Language page (×2) and the Transcribe flyout
- `src/Parlotype.Desktop/Services/Gemma4ModelDownloadDialogService.cs` — shows the shared `ModelDownloadDialog` (via `ModelDownloadViewModel.ForGemma4Model`) to download a Gemma 4 variant's GGUF + mmproj with progress (ADR-029); mirrors `ModelDownloadDialogService` (Whisper)
- `src/Parlotype.Desktop/ViewModels/Gemma4ModelDisplayItem.cs` — per-variant row VM (`IsSelected`, `IsInstalled`, Select/Download/Delete commands)
- `src/Parlotype.Desktop/Services/ParakeetModelDownloadDialogService.cs` + `ViewModels/ParakeetModelDisplayItem.cs` — Parakeet mirror of the Gemma 4 download dialog + row VM (`ModelDownloadViewModel.ForParakeetModel`; 4 files reported as one combined download — ADR-041)
- `src/Parlotype.Desktop/ViewModels/PromptDisplayItem.cs` — per-prompt row VM (`IsSelected`, `IsBuiltIn`, `CanEdit`, Select/Edit/Duplicate/Delete commands)
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsSectionViewModelBase.cs` — base abstract class; each section advertises `Title`, `Category` (`Audio` / `SpeechEngine` / `Input` / `Appearance`), and optional `RestrictToEngine` for engine-scoped sections (ADR-028)
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsCategory.cs` — category enum + `GetDisplayName` extension used for group headers in the navigation pane
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsNavItem.cs` — flat-list nav row model; either a non-selectable header (`IsHeader=true`) or a section row; consumed by the left `ListBox`
- `src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml` — sections: Active server (status + Managed/Manual badge + /props readout), Update banner, Installed (RadioButton + colored backend chip + Uninstall per row), Manual install (distinct background + "Not managed by Parlotype" badge), Available builds (per-row Install button, badge swap when already installed), port + Save/Reset
- `src/Parlotype.Desktop/Views/SettingsWindow.axaml` — `SplitView` + `ListBox` (grouped nav with non-selectable headers via `navHeader` class) + `ContentControl` with `Window.DataTemplates` mapping each section VM type to its `UserControl`
- `src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs` — projects all section VMs into `NavItems` (group headers + visible sections) filtered by active engine; rebuilds on engine change (subscribes to `SpeechEngineSettingsViewModel.SelectedEngine`); preserves selection across rebuilds; `OnSelectedNavItemChanged` auto-fires `LlamaCppSettingsViewModel.RefreshServerInfoCommand` whenever the user navigates to the llama.cpp section, so the `/health`+`/props` probe runs without a manual Refresh click
- `src/Parlotype.Desktop/Views/Settings/` — per-section UserControls
- `src/Parlotype.Desktop/Assets/parlotype.ico` — tray icon

## Launch

```bash
dotnet run --project src/Parlotype.Desktop
```

App starts hidden — only the tray icon is visible. Click the tray icon to open the menu (Open / Settings / Exit). The global hotkey opens the Transcribe window AND starts recording.

## Conventions

- Avalonia 12.0.2 (GA). Classic `Avalonia.Diagnostics` is retired in Avalonia 12 — not referenced. The official replacement (`AvaloniaUI.DiagnosticsSupport` + `avdt` global tool) is wired in DEBUG builds; see Diagnostics section below.
- `OnLostFocus(FocusChangedEventArgs)` is the Avalonia 12 signature (was `RoutedEventArgs` in 11).
- `x:CompileBindings="True"` and `x:DataType` on all AXAML.
- `[ObservableProperty]` / `[RelayCommand]` (CommunityToolkit.Mvvm) — ViewModels are `partial`.
- All ViewModels and `IWindowManager` / `HotkeyCoordinator` are singletons.
- Settings persist to `%LOCALAPPDATA%/parlotype/settings.json`.
- Logs to `parlotype-{date}_{seq}.log`.

## Tests

`Parlotype.Desktop.Tests` — Avalonia.Headless.XUnit 12.0.2 + **xUnit v3** (3.2.2). Tests touching `Dispatcher.UIThread` must use `[AvaloniaFact]`. Async tests must thread `TestContext.Current.CancellationToken` to satisfy analyzer rule `xUnit1051`. Mocks for ADR-026's llama-server install surface live under `Parlotype.Desktop.Tests/Mocks/`: `MockLlamaServerCatalog` scripts the release groups returned to the VM; `MockLlamaServerInstaller` delegates id derivation to the real `LlamaServerInstaller.BuildInstallId` so VM matching against installed entries works.

## Diagnostics (Avalonia 12 DevTools)

`Parlotype.Desktop.csproj` references `AvaloniaUI.DiagnosticsSupport` 2.2.1 conditionally on `Configuration == Debug`. `App.Initialize()` calls `this.AttachDeveloperTools()` under `#if DEBUG`. Release builds carry no extra binaries.

To use locally:

```bash
dotnet tool install --global AvaloniaUI.DeveloperTools   # one-time per developer
avdt                                                      # launch separate inspector
dotnet run --project src/Parlotype.Desktop -c Debug   # then F12 in the window
```

First-time activation needs a free AvaloniaUI Portal account (Essentials edition is free for orgs under €1M revenue). See ADR [[016-avalonia12-developer-tools]].

## History

V1 (`Parlotype.Desktop`, Avalonia 11) was retired in ADR-018. This project was originally named `Parlotype.Desktop.V2` and renamed to `Parlotype.Desktop` after the V1 sunset.
