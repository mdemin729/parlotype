---
title: Parlotype.Desktop.V2
type: service-profile
status: active
tags: [desktop, avalonia, avalonia12, tray, ui, mvvm]
criticality: medium
last_updated: 2026-05-12
summary: Avalonia 12 tray-based desktop frontend — parallel to Parlotype.Desktop
---

# Parlotype.Desktop.V2

## Purpose

Tray-first desktop frontend on Avalonia 12.0.2 (GA). Coexists with V1 (`Parlotype.Desktop`); same `Parlotype.Core` + `Parlotype.Platform` are reused unchanged. See ADR [[015-parlotype-desktop-v2-avalonia12]].

## Key Paths

- `src/Parlotype.Desktop.V2/App.axaml(.cs)` — TrayIcon + NativeMenu, DI bootstrap, `ShutdownMode.OnExplicitShutdown`
- `src/Parlotype.Desktop.V2/Services/IWindowManager.cs` + `WindowManager.cs` — single-instance Transcribe + Settings windows; `Closing` handler hides instead of closes
- `src/Parlotype.Desktop.V2/Services/HotkeyCoordinator.cs` — bridges `IGlobalHotkeyService` → `ShowTranscribe()` + `StartRecordingAsync()`
- `src/Parlotype.Desktop.V2/Services/SilentModelDownloadService.cs` — `IModelDownloadService` impl that downloads Whisper models silently in the background (no dialog; V2 has no always-visible main window)
- `src/Parlotype.Desktop.V2/ViewModels/AppViewModel.cs` — tray menu commands (Open / Settings / Exit), bound from `Application.DataContext`
- `src/Parlotype.Desktop.V2/ViewModels/Settings/` — `MicrophoneSettingsViewModel`, `WhisperModelSettingsViewModel`, `HotkeySettingsViewModel`, `ThemeSettingsViewModel`
- `src/Parlotype.Desktop.V2/Views/SettingsWindow.axaml` — `SplitView` + `ListBox` + `ContentControl` with `Window.DataTemplates` mapping each section VM type to its `UserControl`
- `src/Parlotype.Desktop.V2/Views/Settings/` — per-section UserControls
- `src/Parlotype.Desktop.V2/Assets/parlotype.ico` — tray icon

## Launch

```bash
dotnet run --project src/Parlotype.Desktop.V2
```

App starts hidden — only the tray icon is visible. Click the tray icon to open the menu (Open / Settings / Exit). The global hotkey opens the Transcribe window AND starts recording.

## Conventions

- Avalonia 12.0.2 (GA). `Avalonia.Diagnostics` has no 12.x release — not referenced.
- `OnLostFocus(FocusChangedEventArgs)` is the Avalonia 12 signature (was `RoutedEventArgs` in 11).
- `x:CompileBindings="True"` and `x:DataType` on all AXAML.
- `[ObservableProperty]` / `[RelayCommand]` (CommunityToolkit.Mvvm) — ViewModels are `partial`.
- All ViewModels and `IWindowManager` / `HotkeyCoordinator` are singletons.
- Settings persist to the same `%LOCALAPPDATA%/parlotype/settings.json` as V1.
- Logs to `parlotype-v2-{date}_{seq}.log` (distinct from V1's `parlotype-`).

## Tests

`Parlotype.Desktop.V2.Tests` — Avalonia.Headless.XUnit 12.0.2 + **xUnit v3** (3.2.2). Tests touching `Dispatcher.UIThread` must use `[AvaloniaFact]`. Async tests must thread `TestContext.Current.CancellationToken` to satisfy analyzer rule `xUnit1051`.

## Differences from V1

| Aspect | V1 (Parlotype.Desktop) | V2 (Parlotype.Desktop.V2) |
|---|---|---|
| Avalonia | 11.3.0 | 12.0.2 |
| Primary UX | Single floating toolbar | Tray menu + Transcribe window |
| Settings | One monolithic `SettingsViewModel` + flyout | Per-section VMs in dedicated window with `SplitView` nav |
| Lifetime | Default (close last window → exit) | `OnExplicitShutdown` (close hides; exit only via tray) |
| Hotkey behaviour | Toggle/PTT recording | Same + opens Transcribe window |
| Test framework | xUnit v2 | xUnit v3 |
