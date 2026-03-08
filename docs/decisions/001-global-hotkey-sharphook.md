---
status: accepted
date: 2026-03-08
---

# 001. Global Hotkey System Using SharpHook TaskPoolGlobalHook

## Context

Parlotype needs system-wide Push-to-Talk (PTT) and toggle workflows so users can initiate dictation from any application without switching windows. Without this, the tool is limited to a standalone transcription app rather than a system-wide productivity utility. The implementation must work cross-platform (Windows and macOS), support configurable key combinations, and suppress consumed hotkey events to prevent the OS from acting on them.

## Decision

We use **SharpHook** (already a project dependency at v6.0.0) with its `TaskPoolGlobalHook` for non-blocking keyboard event dispatch. The architecture follows the existing Core → Platform → Desktop layering:

- **Core** defines platform-agnostic models: `HotkeyBinding` (record with `HotkeyModifiers` flags + string key name), `ActivationMode` enum (PushToTalk / Toggle), `HotkeyConflictDetector` (curated reserved-shortcut list), and the extended `IGlobalHotkeyService` interface.
- **Platform** implements `SharpHookHotkeyService` using `TaskPoolGlobalHook` with `GlobalHookType.Keyboard`. An internal `KeyCodeMapper` handles bidirectional mapping between readable key names (e.g. `"Space"`) and SharpHook `KeyCode` values. Matched events set `SuppressEvent = true`.
- **Desktop** provides a `HotkeyRecorderView` UserControl (captures next keypress combination) and wires PTT/Toggle into `MainWindowViewModel` (hotkey → audio pipeline → transcription → text injection).

Key design choices:

1. **`TaskPoolGlobalHook` over `SimpleGlobalHook`** — dispatches events to the thread pool rather than blocking a dedicated thread, avoiding stalls if event handlers are slow.
2. **String-based key names in Core** — keeps `Parlotype.Core` free of SharpHook dependencies; the mapping lives entirely in Platform's `KeyCodeMapper`.
3. **PTT as primary mode** — key-down starts recording, key-up stops it. Toggle mode is an opt-in alternative.
4. **Settings persistence** — hotkey binding and activation mode are stored as separate keys (`HotkeyModifiers`, `HotkeyKey`, `ActivationMode`) in `JsonSettingsService`, matching the existing settings pattern.
5. **Conflict detection in Core** — a static curated list of reserved OS shortcuts (Win+L, Ctrl+Alt+Delete, Cmd+Space, etc.) warns users but does not block assignment.

## Consequences

**Easier:**

- Adding new activation modes (e.g., voice-activity-triggered) only requires extending the `ActivationMode` enum and adding a branch in `SharpHookHotkeyService`.
- Changing the default hotkey is a one-line change in `HotkeyBinding.Default`.
- The `HotkeyRecorderView` pattern can be reused for any future shortcut configuration (e.g., text correction trigger).
- Testing is straightforward via `MockGlobalHotkeyService` — no real keyboard hooks needed in tests.

**More difficult:**

- SharpHook's `SuppressEvent` only works on Windows and macOS; Linux will pass the hotkey through to the OS.
- The conflict detector uses a static curated list rather than querying the OS for active shortcuts, so it may miss third-party application conflicts.
- macOS requires the user to grant Accessibility permissions before the global hook can function; this is not yet handled in the UI (a future task).
