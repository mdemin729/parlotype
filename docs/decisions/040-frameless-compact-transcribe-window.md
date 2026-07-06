# ADR-040: Frameless Compact Transcribe Window

## Status

Accepted

## Context

The Transcribe window (240×184, system title bar) is a floating always-on-top widget the user keeps over other applications while dictating. The title bar added visual noise and ~30 px of height, the window could only be dragged by that bar, and the overall footprint obscured the application being dictated into. A design exploration ([plans/2026-07-05-transcribe-window-compact-redesign](../../plans/2026-07-05-transcribe-window-compact-redesign/task.md)) produced four HTML prototypes; the user selected **C2 — mini card with a grip strip**, modelled on the Windows Voice Typing (Win+H) widget.

## Decision

### Frameless chrome

- `TranscribeWindow` sets `WindowDecorations="None"` (Avalonia 12 API; `SystemDecorations` is obsolete and fails the build under `TreatWarningsAsErrors`), `TransparencyLevelHint="Transparent"`, and a transparent window background.
- The visible surface is a root `Border` ("RootChrome") with `CornerRadius="12"`, a 1 px theme border, and `SystemControlBackgroundAltHighBrush` fill — the same chrome recipe as `Border.popoverChrome`, so light/dark themes work unchanged.
- Fixed size **172×118** (~53 % of the previous footprint); `CanResize="False"`, `Topmost="True"`, and `Title` retained for Alt-Tab/taskbar identity.

### Grip-strip dragging (not drag-anywhere)

- A 24 px header zone hosts a centred 40×4 grip pill and a small ✕ button, mirroring Windows Voice Typing. Its `PointerPressed` handler calls `Window.BeginMoveDrag(e)` on left-press; the rest of the window does **not** drag (an earlier drag-anywhere requirement was explicitly reversed by the user).
- On Windows `BeginMoveDrag` enters the modal move loop and returns when the drag ends, so the position is persisted immediately after it returns.

### Hide, don't close

- The ✕ button and `Esc` hide the window (recording continues; the tray icon reopens it) — consistent with `WindowManager`'s existing cancel-`Closing`-and-`Hide()` behaviour.

### Position persistence — a separate window-state store

- Window position is transient chrome state, not a user-configured setting, and it saves far more often (every drag) than anything in `settings.json`. Routing it through `ISettingsService` would mean every drag rewrites the *entire* settings file (locks + reloads + re-serializes transcription/model/hotkey settings alongside two throwaway coordinates) — a correctness-neutral but wasteful and conceptually wrong coupling, flagged in code review.
- New `IWindowStateService` (Core) mirrors `ISettingsService`'s shape (`GetAsync<T>`/`SetAsync<T>`) but is backed by its own file, `%LOCALAPPDATA%/parlotype/window-state.json` (`JsonWindowStateService`, Platform), registered alongside `ISettingsService` in `PlatformServiceExtensions`. Both concrete services share a `JsonFileStore` base (extracted from the original `JsonSettingsService`) that owns the file path, an instance-level `SemaphoreSlim` lock, and the JSON load/save logic — so the two stores never contend with or corrupt each other's file.
- The position is a single `WindowPosition(int X, int Y)` record struct stored under one key, `WindowStateKeys.TranscribeWindowPosition` — one `GetAsync`/`SetAsync` round trip instead of the original two-key (`PosX`/`PosY`) design, which doubled the lock/I/O cost for values that are never independent.
- `TranscribeWindow.RestorePositionAsync(IWindowStateService)` — called by `WindowManager` before the first `Show()` — applies the saved position via `WindowStartupLocation.Manual` only when the window rect still intersects a connected screen (`Screens.All`); otherwise the `CenterScreen` default stands (monitor unplugged / resolution change). Verified empirically in headless tests: the headless platform reports a real virtual screen (not "no screen info"), so the intersection check runs for real, not just the trust-blindly fallback.
- `SavePositionAsync()` runs after a grip drag completes, on hide (✕/Esc), and on `Closing`.

### Status text

- The always-visible status `TextBlock` is gone. `StatusText` is now surfaced as `ToolTip.Tip` on the root chrome, so hovering the widget shows "Ready" / "Recording…" / model-load progress. The record button itself already signals recording (blue chrome), loading (spinner), and speech (waveform).

## Consequences

- The widget occupies less than half its previous area and no longer has OS chrome; all functionality (record + waveform states, settings, language strip + flyout) is preserved in the tighter layout.
- Users must discover the grip strip to move the window; the `SizeAll` cursor and the familiar Voice-Typing pattern mitigate this.
- Position persists across restarts; a stale off-screen position self-heals to centre-screen.
- Headless coverage: `TranscribeWindowChromeTests` (decorations/size, grip+✕ presence, Esc hides, restore/save/off-screen-fallback); `JsonFileStoreTests` (Platform) covers the shared store's round-trip, missing-key default, struct serialization, and file-isolation behaviour via a temp-path test subclass — real user AppData is never touched by tests. Screenshot scenarios re-render the new chrome. `BeginMoveDrag` itself is untestable headlessly — drag behaviour needs a manual pass on Windows.
- Minor API surface: `RestorePositionAsync`/`SavePositionAsync` are public on the window purely so `WindowManager` and tests can drive persistence.
- Adds a second small file (`window-state.json`) under `%LOCALAPPDATA%/parlotype/` alongside `settings.json` — an intentional trade-off so window-drag persistence never touches user-configured settings.
