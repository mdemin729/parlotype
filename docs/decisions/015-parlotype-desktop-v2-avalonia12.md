---
status: accepted
date: 2026-05-12
---

# 015. Parlotype.Desktop.V2 — Avalonia 12 Tray-Based UI

## Context

The original `Parlotype.Desktop` is a single floating-toolbar window built on Avalonia 11.3. We wanted to explore a tray-first UX (Open / Settings / Exit menu, dedicated Transcribe and Settings windows) on the latest Avalonia line, without disrupting the existing app or its users.

## Decision

Introduce a parallel desktop frontend `Parlotype.Desktop.V2` targeting **Avalonia 12.0.2 (GA)**, alongside `Parlotype.Desktop`. Both projects coexist in the solution and share `Parlotype.Core` + `Parlotype.Platform` unchanged.

**Key choices:**

- **Tray icon** via the built-in `Avalonia.Controls.TrayIcon` declared in `App.axaml`. The tray menu (Open / Settings / Exit) is bound to commands on `AppViewModel` (set as `Application.DataContext`).
- **Lifetime** uses `ShutdownMode.OnExplicitShutdown` and no `MainWindow` — closing windows hides them; the app exits only via the tray menu's Exit item.
- **Window manager** (`IWindowManager` / `WindowManager`) owns single-instance Transcribe and Settings windows. The `Closing` event handler calls `e.Cancel = true; window.Hide()` so the windows persist behind the tray.
- **Settings navigation** — `SplitView` + `ListBox` + `ContentControl` with `Window.DataTemplates` mapping each section ViewModel type to its `UserControl`. Sections: Microphone, Whisper Model, Hotkey, Theme.
- **Per-section ViewModels** carved out of V1's monolithic `SettingsViewModel`: `MicrophoneSettingsViewModel`, `WhisperModelSettingsViewModel`, `HotkeySettingsViewModel`, `ThemeSettingsViewModel`. A `SettingsWindowViewModel` aggregates them and drives navigation.
- **Hotkey behavior** — `HotkeyCoordinator` subscribes to `IGlobalHotkeyService.HotkeyPressed/Released`, dispatches to `Dispatcher.UIThread` to show + activate the Transcribe window AND start recording on press (PTT release stops it).
- **Settings persistence** — V2 reuses the existing `SettingsKeys` constants and the same `%LOCALAPPDATA%/parlotype/settings.json` file as V1; no migration needed.
- **Model download** — `SilentModelDownloadService` (V2-local) implements `IModelDownloadService` by wrapping `HttpModelDownloadService` and downloading without UI. V2 is tray-first and has no always-visible main window to host the V1 modal download dialog. `Parlotype.Platform` does not register `IModelDownloadService` itself; each frontend (Desktop V1, Desktop V2, Benchmark) supplies its own implementation.
- **Tests** — `Parlotype.Desktop.V2.Tests` uses `Avalonia.Headless.XUnit 12.0.2` with **xUnit v3** (not v2) because the headless package transitively requires `xunit.v3.extensibility.core`. `[AvaloniaFact]` is required for any test that touches `Dispatcher.UIThread`.

## Consequences

**Positive:**
- Tray-first UX matches OS conventions (Windows, macOS, Linux all supported by `TrayIcon`).
- Per-section ViewModels are simpler, more testable, and easier to extend than the V1 monolith.
- V1 remains untouched; users can run either frontend (`dotnet run --project src/Parlotype.Desktop` vs `…V2`).

**Negative:**
- Solution now ships two desktop frontends — must be kept in sync until V1 is retired.
- `Avalonia.Diagnostics` has no 12.x package (latest is 11.3.14), so the V2 dev experience lacks the visual tree inspector. Tracked for future restoration.
- xUnit v3 in V2.Tests differs from V1's xUnit v2 — analyzer rules (e.g. `xUnit1051`) require `TestContext.Current.CancellationToken` to be threaded through async APIs.
- `OnLostFocus(RoutedEventArgs)` → `OnLostFocus(FocusChangedEventArgs)` is a breaking signature change in Avalonia 12; any code-behind override must be updated.

## Alternatives Considered

- **Replace V1 in-place** — rejected: high blast radius, no fallback if Avalonia 12 regresses for our users.
- **Keep one monolithic SettingsViewModel** — rejected: navigation panel demands per-section state and lifecycle isolation.
- **Build a custom system-tray helper** — rejected: `TrayIcon` ships in-box, cross-platform.
- **Stay on Avalonia 11.3** — rejected: the goal of the work was to pilot Avalonia 12 on a real surface.

## References

- `src/Parlotype.Desktop.V2/App.axaml` (TrayIcon + NativeMenu)
- `src/Parlotype.Desktop.V2/Services/WindowManager.cs`
- `src/Parlotype.Desktop.V2/Services/HotkeyCoordinator.cs`
- `src/Parlotype.Desktop.V2/Views/SettingsWindow.axaml` (SplitView nav)
- `src/Parlotype.Desktop.V2.Tests/Parlotype.Desktop.V2.Tests.csproj` (xunit.v3 + Avalonia.Headless.XUnit 12.0.2)
- ADR [[010-avalonia-headless-ui-testing]] — supersedes nothing; V2 tests follow the same headless pattern but on xUnit v3.
