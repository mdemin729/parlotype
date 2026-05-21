---
title: Global Hotkey & Input System
status: completed
created: 2026-03-08
started: 2026-03-08
completed: 2026-03-08
---

# Global Hotkey & Input System

## Problem

Parlotype needs a system-wide "Push-to-Talk" (PTT) or toggle workflow so users can initiate dictation from any active application without switching windows. Without this, the tool is limited to a standalone transcription app rather than a system-wide productivity utility.

## Requirements

### Global Input Capture

- **System-Wide Hook:** Monitor keyboard events even when Parlotype is not the focused window.
- **Event Suppression:** Consume hotkey events to prevent the host OS from performing default actions (e.g., prevent window menu on `Alt+Space`).
- **Cross-Platform:** Must work on Windows (Win32/SendInput) and macOS (Accessibility API / CGEventPost).

### Activation Modes

- **Push-to-Talk (PTT):** Recording starts on KeyDown, stops on KeyUp.
- **Toggle Mode (optional):** Single tap starts recording, second tap stops it.

### Hotkey Customization

- **Default Combinations:** `Ctrl+Shift+Space` and `Alt+Space` as out-of-the-box defaults.
- **User Mapping:** Users can record custom combinations including modifiers (Ctrl, Alt, Shift, Win) and a primary key.
- **Conflict Detection:** Warn users if they attempt to map a reserved system shortcut (e.g., `Win+L`, `Win+E`).

### Text Injection (Output)

- **Simulated Keystrokes:** After transcription (and optional LLM post-processing), automatically type text into the previously active text field.
- **Clipboard Fallback:** Option to copy text to clipboard instead of direct injection.

## Technical Constraints

### Implementation Stack

| Component | Technology |
|-----------|-----------|
| Global hooks | SharpHook (C# wrapper for libuiohook) |
| UI framework | Avalonia UI |
| Text injection | `SendInput` (Windows) / `CGEventPost` (macOS) |

### OS Nuances

| Aspect | Windows | macOS |
|--------|---------|-------|
| API | Win32 / SendInput | Accessibility API / CGEventPost |
| Permissions | None for standard apps; Admin for admin-level windows | Must prompt user to enable Accessibility in System Settings |
| Injection | SendInput for foreground window | CGEventPost within Accessibility framework |

## UI Requirements

### Configuration Screen

- **Hotkey Recorder:** Dedicated input field that captures the next keypress combination.
- **Permission Status:** Visual indicator (especially macOS) showing whether system permissions are granted.

### Feedback Loop

- **Visual Indicator:** Non-intrusive overlay or system tray icon state change (e.g., red = listening).
- **Auditory Cue (optional):** Sound on recording start/stop for eyes-free confirmation.

## Security & Privacy

- **No Keylogging:** Only act on the configured hotkey. No other keystrokes are recorded, stored, or transmitted.
- **Local Processing:** All hotkey logic and transcription occur entirely offline on-device.

## Approach

Leverage the existing SharpHook integration in `Parlotype.Platform/Hotkeys/` and text injection services in `Parlotype.Platform/TextInjection/`. The global hook is already partially scaffolded via the `Hotkeys` contracts in `Parlotype.Core/Hotkeys/`.

Key design decisions:
1. Use SharpHook's `TaskPoolGlobalHook` for non-blocking event dispatch
2. Implement PTT as the primary mode; toggle mode as a follow-up
3. Hotkey configuration persisted via the existing `JsonSettingsService`
4. Conflict detection against a curated list of known OS-reserved shortcuts
5. Extend existing `ClipboardTextInjectionService` / `SharpHookTextInjectionService` for the output path

## Workplan

- [ ] Audit existing `Parlotype.Core/Hotkeys/` and `Parlotype.Platform/Hotkeys/` contracts and implementations
- [ ] Define/refine `IGlobalHotkeyService` interface in Core with PTT semantics (KeyDown → start, KeyUp → stop)
- [ ] Implement `SharpHookGlobalHotkeyService` in Platform using `TaskPoolGlobalHook`
- [ ] Add event suppression for consumed hotkey events
- [ ] Implement hotkey configuration model and persistence via `JsonSettingsService`
- [ ] Add conflict detection for reserved OS shortcuts
- [ ] Build hotkey recorder UI component (captures next keypress combination)
- [ ] Add PTT integration: wire hotkey events → audio capture start/stop → transcription → text injection
- [ ] Add visual feedback (tray icon state change or overlay) during recording
- [ ] Add optional auditory cue on recording start/stop
- [ ] Add toggle mode as an alternative activation mode
- [ ] Write unit tests for hotkey service, conflict detection, and configuration
- [ ] Write headless UI tests for hotkey recorder component
- [ ] Test cross-platform behavior (Windows + macOS permission prompting)
