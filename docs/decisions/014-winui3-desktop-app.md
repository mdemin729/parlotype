---
status: accepted
date: 2026-04-05
decision-makers: [maxim]
---

# ADR-014: WinUI 3 Native Windows Desktop Application

## Context

Parlotype's UI layer is built with Avalonia UI 11.3, a cross-platform XAML framework. While Avalonia provides excellent cross-platform reach, a native Windows experience using WinUI 3 (Windows App SDK) offers first-class Fluent Design, Mica/Acrylic backdrops, system tray integration, and better Windows-specific behaviors.

The goal is to create a parallel WinUI 3 application without removing the Avalonia version, sharing the same Core and Platform layers.

## Decision

### New Project: `Parlotype.WinUI`

- **Framework:** WinUI 3 via Windows App SDK 1.8 (`Microsoft.WindowsAppSDK` 1.8.260317003)
- **TFM:** `net10.0-windows10.0.19041.0` (overrides the solution-wide `net10.0` in `Directory.Build.props`)
- **Packaging:** Unpackaged (`WindowsPackageType=None`) — no MSIX, runs directly from build output
- **Self-contained:** `WindowsAppSDKSelfContained=true` when building for a specific platform (x64/ARM64)
- **Architecture:** References `Parlotype.Core` and `Parlotype.Platform` directly — same audio pipeline, hotkeys, settings persistence, and Whisper engine

### Architecture: Tray-Resident Application

Unlike the Avalonia app (which opens a main window immediately), the WinUI app is tray-resident:

1. **System Tray Icon** — `H.NotifyIcon.WinUI` 2.4.1 provides the `TaskbarIcon` with context menu
2. **Tray Menu:** Open (shows Transcribe window), Settings (shows Settings window), Exit
3. **TranscribeWindow** — Compact chromeless floating window (~300×200) with Play/Stop and Settings buttons, using Mica backdrop and extended title bar
4. **SettingsWindow** — Standard 800×600 window with `NavigationView` shell and four pages:
   - Audio (microphone selection, silence timeout)
   - Speech Model (Whisper model selection with cache/download status)
   - Hotkeys (hotkey recorder with conflict detection, activation mode)
   - Appearance (theme: Default/Light/Dark)
5. **Global Hotkey** — Reuses `SharpHookHotkeyService` from Platform; shows/activates TranscribeWindow

### Shared Components

| Component | Source | Notes |
|-----------|--------|-------|
| Audio pipeline | `Parlotype.Platform` | WASAPI capture, Silero VAD, Whisper.net |
| Global hotkeys | `Parlotype.Platform` | SharpHook TaskPoolGlobalHook |
| Settings persistence | `Parlotype.Platform` | `JsonSettingsService` → `%LOCALAPPDATA%/parlotype/settings.json` |
| MVVM toolkit | NuGet | `CommunityToolkit.Mvvm` 8.4.0 — framework-agnostic |
| Logging | NuGet | ZLogger to console + rolling file |

### Key Technical Decisions

- **ViewModels are not shared** between Avalonia and WinUI apps. While CommunityToolkit.Mvvm is framework-agnostic, the binding patterns and UI-thread dispatching differ enough that separate VM implementations are cleaner.
- **Windows are created lazily** and set to null on close, allowing garbage collection. State lives in singleton ViewModels.
- **No `Program.cs`** — WinUI 3 apps use the generated entry point from the SDK.
- **MVVMTK0045 suppressed** — The partial-property warning is a WinRT AOT concern; unpackaged desktop apps work fine with field-based `[ObservableProperty]`.

## Consequences

### Positive

- Native Fluent Design with Mica/Acrylic backdrops
- First-class system tray integration
- Shared Core/Platform code — no logic duplication
- Independent deployment (no MSIX required)
- Existing Avalonia app remains functional for cross-platform use

### Negative

- Two UI layers to maintain (Avalonia + WinUI)
- WinUI project requires platform-specific build (`-p:Platform=x64`)
- `Directory.Build.props` TFM override needed for WinUI project
- System tray requires third-party library (H.NotifyIcon) since WinUI has no native tray support
