---
title: "Session: 2026-05-02 — Topmost transcribe window"
type: session
status: complete
tags: [desktop, avalonia, window-management, topmost, focus]
created: 2026-05-02
summary: "Made TranscribeWindow always-on-top and prevented focus stealing on hotkey activation"
---

# Session: 2026-05-02 — Topmost Transcribe Window

## Active Focus
- `src/Parlotype.Desktop/Views/TranscribeWindow.axaml` — added `Topmost="True"`
- `src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml` — added `Topmost="True"` (code review catch: dialog would render behind topmost parent)
- `src/Parlotype.Desktop/Services/IWindowManager.cs` — added `bool activate = true` parameter to `ShowTranscribe()`
- `src/Parlotype.Desktop/Services/WindowManager.cs` — conditional `ShowActivated` + `Activate()` based on `activate` param
- `src/Parlotype.Desktop/Services/HotkeyCoordinator.cs` — calls `ShowTranscribe(activate: false)` to avoid focus steal
- Updated stubs: `AppViewModel.DesignWindowManager`, `TranscribeViewModel.DesignWindowManager`, `MockWindowManager`

## Decisions Made
- **Topmost on TranscribeWindow**: User requested always-on-top for the transcription overlay
- **Topmost on ModelDownloadDialog**: Proactive fix from code review — a non-topmost dialog parented to a topmost window can render behind it on some platforms
- **Optional `activate` parameter (default true)**: Keeps tray/menu activation behavior intact while letting the hotkey path opt out of focus stealing. Default parameter avoids breaking existing callers.
- **`ShowActivated = false` + skip `Activate()`**: Avalonia's `ShowActivated` property prevents `Show()` from taking focus; skipping `Activate()` ensures no focus change at all

## Facts Learned
- Avalonia `Window.ShowActivated = false` prevents `Show()` from stealing focus — standard API, no quirks discovered
- Avalonia `ShowDialog(owner)` with a topmost owner may not reliably make the dialog topmost on all platforms — safer to set `Topmost` explicitly on both

## Open Blockers
- None

## Documentation Status
- ADR: none required — no new Core types, no new dependencies, no subsystem-level change
- Vault (services/architecture): done — updated `memory/services/desktop.md` (IWindowManager activate param), fixed `memory/AGENTS.md` launch command
- Knowledge (non-derivable facts): none — all facts are derivable from Avalonia docs

## Next Action
- Verify topmost + no-focus-steal behavior manually: `dotnet run --project src/Parlotype.Desktop`, press hotkey while typing in another app, confirm window appears without losing cursor position
- Consider whether `SettingsWindow` should also be topmost (currently not — opens via explicit user action from transcribe window)
- Consider implementing `LinuxPortalHotkeyService` using `xdg-desktop-portal` GlobalShortcuts D-Bus API (carried from previous session)
