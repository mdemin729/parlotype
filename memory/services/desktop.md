---
title: Parlotype.Desktop
type: service-profile
status: active
tags: [desktop, avalonia, avalonia12, tray, ui, mvvm]
criticality: medium
last_updated: 2026-05-22
summary: Avalonia 12 tray-based desktop frontend — sole desktop app
---

# Parlotype.Desktop

## Purpose

Tray-first desktop frontend on Avalonia 12.0.2 (GA). Sole desktop app after V1 sunset (ADR-018). Reuses `Parlotype.Core` + `Parlotype.Platform`. See ADR [[015-parlotype-desktop-avalonia12]].

## Key Paths

- `src/Parlotype.Desktop/App.axaml(.cs)` — TrayIcon + NativeMenu, DI bootstrap, `ShutdownMode.OnExplicitShutdown`
- `src/Parlotype.Desktop/Services/IWindowManager.cs` + `WindowManager.cs` — single-instance Transcribe + Settings windows; `Closing` handler hides instead of closes; `ShowTranscribe(activate: false)` shows without stealing focus (used by hotkey path)
- `src/Parlotype.Desktop/Services/HotkeyCoordinator.cs` — bridges `IGlobalHotkeyService` → `ShowTranscribe()` + `StartRecordingAsync()`
- `src/Parlotype.Desktop/Services/SilentModelDownloadService.cs` — `IModelDownloadService` impl that downloads Whisper models silently in the background (no dialog; tray app has no always-visible main window)
- `src/Parlotype.Desktop/Services/LlamaServerInstallDialogService.cs` — `ILlamaServerInstaller` wrapper that opens the generalized `ModelDownloadDialog` with phase-aware status text (downloading / downloading-companion / verifying / extracting / finalizing). Registered after `AddPlatformServices()` so its `AddSingleton<ILlamaServerInstaller, ...>` wins via last-registration semantics; the concrete `LlamaServerInstaller` is still resolvable for Benchmark / headless paths (ADR-026)
- `src/Parlotype.Desktop/ViewModels/AppViewModel.cs` — tray menu commands (Open / Settings / Exit), bound from `Application.DataContext`
- `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs` — recording state machine (`RecordingState`, `AudioLevel`, `IsIdle`, `IsActive`), EMA-smoothed RMS with 1200ms hold-off for stable Active/Idle transitions
- `src/Parlotype.Desktop/Views/WaveformView.cs` — custom `Control` rendering three states: mic icon (Disabled), breathing bars (Idle), animated multi-frequency wave (Active); 60fps `DispatcherTimer`; white bars on blue button background
- `src/Parlotype.Desktop/ViewModels/Settings/` — `SpeechEngineSettingsViewModel` (Whisper/Gemma4 toggle), `MicrophoneSettingsViewModel`, `SilenceTimeoutSettingsViewModel` (wait time, engine-agnostic — Audio category), `WhisperModelSettingsViewModel` (coordinates model hot-swap: stops recording + unloads model; model list shows a "no translation" hint per `SupportsTranslation`), `RuntimeSettingsViewModel` (CUDA/Vulkan/CPU pinning for Whisper), `WhisperOutputSettingsViewModel` (punctuation, profanity, translate to English — Whisper-restricted; `CanTranslate` disables the translate toggle when the selected model can't translate, preserving the saved preference — `SettingsWindowViewModel` calls `UpdateTranslationAvailability` on model change, ADR-033), `Gemma4ModelSettingsViewModel` (5-model catalog pick keyed by `ModelId` + per-model Download/Delete, SpeechEngine category, Gemma4-restricted, ADR-029), `PromptSettingsViewModel` (create/edit/duplicate/delete/select transcription prompts via `IPromptTemplateRegistry`; inline `IsEditing` form; built-ins non-editable; SpeechEngine category, Gemma4-restricted, ADR-030), `HotkeySettingsViewModel`, `ThemeSettingsViewModel`, `LlamaCppSettingsViewModel` (managed install browse/install/uninstall/set-active + manual folder picker, ADR-026), `LlamaServerInstallRowVm` / `LlamaServerVariantRowVm` / `LlamaServerBackendFormatter` (row VMs + display helpers for the llama settings page)
- `src/Parlotype.Desktop/Services/Gemma4ModelDownloadDialogService.cs` — shows the shared `ModelDownloadDialog` (via `ModelDownloadViewModel.ForGemma4Model`) to download a Gemma 4 variant's GGUF + mmproj with progress (ADR-029); mirrors `ModelDownloadDialogService` (Whisper)
- `src/Parlotype.Desktop/ViewModels/Gemma4ModelDisplayItem.cs` — per-variant row VM (`IsSelected`, `IsInstalled`, Select/Download/Delete commands)
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
