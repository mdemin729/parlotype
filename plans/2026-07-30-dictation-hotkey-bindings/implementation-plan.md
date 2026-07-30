# Implementation plan: multi-binding dictation hotkeys

Companion to [task.md](task.md) (feature set) and [research.md](research.md)
(competitive analysis). This document maps the feature set onto the existing
code and records the design decisions and open questions.

## 1. Current state (what actually exists)

| Piece | File | Behaviour today |
|---|---|---|
| Binding model | `src/Parlotype.Core/Hotkeys/HotkeyBinding.cs` | Single record `(HotkeyModifiers, string Key)`; `IsValid` **requires** ≥1 modifier + non-modifier key — bare-modifier gestures are unrepresentable. `Default = Ctrl+Shift+Space` |
| Mode | `src/Parlotype.Core/Hotkeys/ActivationMode.cs` | Global PTT-vs-Toggle switch on `IGlobalHotkeyService.Mode` |
| Service contract | `src/Parlotype.Core/Hotkeys/IGlobalHotkeyService.cs` | `HotkeyPressed`/`HotkeyReleased` (plain `EventHandler`), one `CurrentBinding`, `UpdateBinding()` |
| Conflict check | `src/Parlotype.Core/Hotkeys/HotkeyConflictDetector.cs` | Static reserved-shortcut list only; no check against Parlotype's own bindings; list lacks Win+H, Win+Ctrl+S, Win+Space |
| Listener | `src/Parlotype.Platform/Hotkeys/SharpHookHotkeyService.cs` | `SimpleGlobalHook` (ADR-020); exact match of target keycode + exact modifier mask; `e.SuppressEvent = true` on match; toggle bookkeeping via `_isToggleRecording` |
| Key mapping | `src/Parlotype.Platform/Hotkeys/KeyCodeMapper.cs` | Name↔`KeyCode` for non-modifier keys only; `IsModifierKey()` exists; `ToHotkeyModifiers(EventMask)` collapses left/right into one flag |
| Coordinator | `src/Parlotype.Desktop/Services/HotkeyCoordinator.cs` | Pressed → show TranscribeWindow + `StartRecordingAsync()`; Released → `StopRecordingAsync()` (which transcribes + injects — there is **no discard path**) |
| Settings UI | `src/Parlotype.Desktop/Views/Settings/HotkeySettingsView.axaml(.cs)`, `ViewModels/Settings/HotkeySettingsViewModel.cs` | Single recorder button (Avalonia `OnKeyDown` capture, skips modifier-only presses), two mode radios, conflict warning text |
| Persistence | `SettingsKeys.HotkeyModifiers` / `HotkeyKey` / `ActivationMode` | Three flat string keys; loaded by both the service and the settings VM |
| Recording widget | `src/Parlotype.Desktop/Views/TranscribeWindow.axaml(.cs)` | Esc **hides to tray** (does not stop or cancel recording); record button has no tooltip (RootChrome tooltip = `StatusText`) |
| Related ADRs | 001 (SharpHook), 020 (SimpleGlobalHook), 039 (PTT stop awaits in-flight start), 040 (frameless widget) | |

## 2. Core model

New files under `src/Parlotype.Core/Hotkeys/`:

```csharp
public enum ModifierKey { Ctrl, Alt, Shift, Meta }
public enum ModifierSide { Either, Left, Right }

public enum HotkeyGestureKind { Chord, HoldModifier, DoubleTapModifier }

/// One binding = a gesture + how it activates dictation.
public sealed record DictationHotkey(HotkeyGesture Gesture, ActivationMode Mode);
```

`HotkeyGesture` is a single serializable record covering all three kinds
(discriminated by `Kind`), keeping `HotkeyBinding` as the chord payload so the
existing display/validation code is reused:

- `Chord`: wraps today's `HotkeyBinding` (modifiers + key).
- `HoldModifier`: `(ModifierKey, ModifierSide)` — e.g. Ctrl/Right.
- `DoubleTapModifier`: `(ModifierKey, ModifierSide)` — side `Either` means
  both taps on the *same physical key* (L-L or R-R, not L-R).

Validation matrix (enforced in the model + settings VM):

| Gesture | Allowed modes |
|---|---|
| HoldModifier | PushToTalk only |
| DoubleTapModifier | Toggle only |
| Chord | PushToTalk or Toggle (default Toggle) |

`DisplayString` per gesture: `"Hold Right Ctrl"`, `"Double-tap Ctrl"`,
`"Ctrl+Alt+Space"`.

**Defaults (fresh install):**

```
1. HoldModifier(Ctrl, Right)        → PushToTalk   (primary)
2. DoubleTapModifier(Ctrl, Either)  → Toggle
3. Chord(Ctrl+Alt, Space)           → Toggle       (explicit fallback)
```

`HotkeyBinding.Default` (Ctrl+Shift+Space) is retired from the default set —
research: it is Parameter Info / signature help in VS and VS Code.

**Cancel is not a user-configurable binding in v1:** `Escape` is hardwired,
matching macOS Dictation and Wispr Flow. The model leaves room to add a
`DictationAction.Cancel` binding list later.

## 3. Gesture state machines (Core, pure, testable)

Two plain classes driven by `(key, isDown, timestampMs)` — no SharpHook types,
so they unit-test in `Parlotype.Tests` without hooks:

- **`ModifierTapTracker`** — a *tap* is modifier-down → modifier-up within
  **250 ms** with **no other key event in between** (otherwise `Ctrl+C`,
  `Ctrl+V` in quick succession reads as a double-tap). Two taps of the same
  physical key within a **350 ms** inter-tap window fire the gesture. Constants
  are `internal` and tunable.
- **`ModifierHoldTracker`** — hold-PTT starts **immediately on key-down**
  (latency matters for dictation). If a non-modifier key goes down while the
  modifier is held **within a 300 ms grace window**, the user was typing a
  shortcut (e.g. RightCtrl+C): abort → emit *cancel* so the just-started
  recording is discarded, and stop suppressing. After the grace window, keys
  pressed during a hold are ignored (the user may legitimately press keys while
  dictating). Key-up ends the hold → *stop*.

Timing constants live with the trackers; the research values (~250 ms tap,
300–400 ms inter-tap) are the starting points.

## 4. Service contract change (Core)

`IGlobalHotkeyService` becomes binding-set-based and semantically-evented:

```csharp
public interface IGlobalHotkeyService : IDisposable
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    /// Semantic dictation events, already resolved from gestures + modes.
    event EventHandler DictationStartRequested;
    event EventHandler DictationStopRequested;
    event EventHandler DictationCancelRequested;   // Escape / hold-abort

    IReadOnlyList<DictationHotkey> Bindings { get; }
    void UpdateBindings(IReadOnlyList<DictationHotkey> bindings);

    /// Fed by the coordinator from TranscribeViewModel.RecordingState so
    /// Escape interception and toggle-stop work regardless of how recording
    /// was started (hotkey or mic button).
    void SetDictationActive(bool active);
}
```

Rationale for `SetDictationActive`: today `_isToggleRecording` is the service's
private guess and desyncs whenever recording starts/stops via the UI button or
errors out (`CloudProviderNotConfiguredException`, runtime failures). The
coordinator owns the truth (it already observes `TranscribeViewModel`) and
pushes it down. Toggle gestures then mean "start if inactive, stop if active",
and Escape is only intercepted while active.

This is a breaking Core interface change → ADR-047. `MockGlobalHotkeyService`
in Desktop.Tests updates accordingly.

## 5. Platform listener rework

`SharpHookHotkeyService`:

- Keeps `SimpleGlobalHook` (ADR-020). All key events fan out to: (a) chord
  matchers, (b) `ModifierTapTracker`, (c) `ModifierHoldTracker`, (d) the
  Escape interceptor.
- **Side-aware codes:** `KeyCodeMapper` gains modifier mappings
  (`VcLeftControl`/`VcRightControl`/… ↔ `ModifierKey`+side). Verify during
  implementation whether SharpHook's `EventMask` exposes left/right separately
  (`EventMask.LeftCtrl` etc.); the trackers key off raw `KeyCode`, not the
  mask, so this only affects the AltGr filter below.
- **Suppression policy (important):**
  - Chord match → `e.SuppressEvent = true` (current behaviour, kept).
  - Bare-modifier events → **never suppressed**, even when they trigger
    hold/double-tap gestures. Suppressing Ctrl-down would break every Ctrl
    shortcut system-wide. Side effect: a Right-Ctrl hold still counts as Ctrl
    for other apps while dictating — acceptable; keys typed mid-hold are
    ignored by us after the grace window anyway.
  - `Escape` → suppressed **only** when dictation is active (it becomes
    cancel); passes through untouched otherwise.
- **AltGr filter:** for chord matching, ignore matches where right-Alt is
  physically down unless the chord explicitly includes Alt — protects European
  layouts where AltGr = Ctrl+Alt (research). Implement by tracking right-Alt
  key state from raw events if the mask can't distinguish sides.
- Toggle semantics move off `_isToggleRecording` onto the
  `SetDictationActive`-fed state (see §4).

## 6. Persistence & migration

- New key `SettingsKeys.HotkeyBindings` — JSON array of `DictationHotkey`
  (System.Text.Json, camelCase, forward-compatible with unknown fields
  ignored).
- **Migration (one-time, on first load when `HotkeyBindings` is absent):**
  - Legacy `HotkeyModifiers`+`HotkeyKey` present *and* ≠ old default
    Ctrl+Shift+Space → the user explicitly recorded a chord: migrate it as
    their single binding with their legacy `ActivationMode`. **Do not** add the
    new defaults on top (no surprise global hotkeys on update).
  - Legacy keys absent *or* equal to the old default → user never customised:
    give them the full new default set (§2).
  - Follow the `TranslateToEnglish` / `RecentLanguages` precedent: legacy keys
    are read once at migration and never written again.
- The service persists the whole set on `UpdateBindings`; the settings VM stops
  writing the legacy keys.

## 7. Conflict validation

`HotkeyConflictDetector` (stays in Core, static):

- New overload `GetConflictDescription(DictationHotkey candidate,
  IReadOnlyList<DictationHotkey> existing)` — flags duplicates and shadowing
  (e.g. a `Hold Right Ctrl` binding plus a chord `RightCtrl`-only… chords
  can't contain bare modifiers, so shadowing reduces to duplicate gestures and
  double-tap-vs-hold on the same modifier+side, which **is** allowed — hold and
  double-tap on the same key are distinguishable by the trackers; document
  this).
- Reserved list additions (from research): `Win+H` (Win11 Voice Typing —
  shell-reserved, unhookable), `Win+Ctrl+S` (Win10 Speech Recognition),
  `Win+Space` and `Ctrl+Win+Space` (input-source switching), `Super+H`
  (GNOME hide window; Meta+H already present for macOS).
- New **warning tier** (accepted but flagged, distinct message): chords that
  collide with common apps rather than the OS — `Ctrl+Shift+Space` (VS/VS Code
  parameter info), any `Ctrl+Alt+<letter>` (AltGr on European layouts —
  message suggests `Ctrl+Alt+Space` instead).

## 8. Desktop: coordinator, cancel path, widget

- **`TranscribeViewModel.CancelRecordingAsync()`** — new: stops audio capture
  and **discards** buffered audio without transcription or injection, returns
  the widget to `Ready`. Must respect the ADR-039 pattern (await the in-flight
  `_startTask` before tearing down, same as `StopRecordingAsync`). The
  audio-service teardown path needs a discard variant — verify whether
  `IAudioCaptureService` stop can simply skip the transcription hand-off in the
  VM (preferred: the discard decision lives in the VM, no Core audio contract
  change).
- **`HotkeyCoordinator`** — subscribes to the three semantic events
  (start/stop/cancel), and observes `TranscribeViewModel.RecordingState`
  (PropertyChanged) to call `SetDictationActive(active)` where
  `active = Loading | Idle-recording | Active` per the VM's state semantics
  (recording in progress, including model-loading — Escape should cancel a
  pending start too).
- **`TranscribeWindow`** Esc handler (`OnKeyDown`): recording → route to
  `CancelRecordingAsync` (window-focused case; the global hook covers the
  unfocused case); idle → `HideToTray()` (current behaviour, ADR-040).
- **Record-button tooltip:** `ToolTip.Tip` on `RecordButton` bound to a new
  `TranscribeViewModel.HotkeyHintText` — derived from the binding set:
  first enabled PTT binding, else first toggle binding, e.g.
  `"Hold Right Ctrl to talk · Esc to cancel"` / `"Double-tap Ctrl to dictate"`.
  Refreshed when bindings change (service exposes a changed event or the VM
  re-reads on settings save). The more specific control tooltip wins over the
  RootChrome `StatusText` tooltip in Avalonia — no conflict.

## 9. Settings UI

`HotkeySettingsView` becomes a **binding list**:

- One list ("Dictation hotkeys"), each row: gesture `DisplayString`, mode label
  (`Push to talk` / `Toggle`), remove button. Global mode radios are removed
  (mode is per-binding now).
- **Add binding** button → flyout menu: `Hold Right Ctrl`, `Hold Right Alt`,
  `Double-tap Ctrl`, `Record a chord…`. Presets exist because an Avalonia
  key-capture field cannot naturally record "double-tap Ctrl" or a bare hold —
  the existing `OnKeyDown` recorder (which deliberately skips modifier-only
  presses) is kept for chords only. Chord rows get a PTT/Toggle choice.
- Inline conflict/warning text per the two-tier detector (§7): reserved →
  binding rejected; warning tier → accepted with amber note (existing
  `ConflictWarning` styling).
- Follow the flyout-lifecycle and display-item conventions from CLAUDE.md
  (commands embedded in display-item wrappers, no `$parent` bindings).

## 10. Test plan

**Parlotype.Tests (Core/Platform):**
- `ModifierTapTracker`: tap recognised; tap rejected when a key intervenes
  (Ctrl+C Ctrl+V sequence must NOT double-tap); tap rejected over 250 ms;
  double-tap within window; L-then-R taps do not pair; triple-tap fires once.
- `ModifierHoldTracker`: start on down / stop on up; abort (cancel) when
  non-modifier key arrives within grace; no abort after grace; left-side key
  does not trigger a Right-side binding.
- Gesture/`DictationHotkey` JSON round-trip; validation matrix (hold+Toggle
  rejected etc.); display strings.
- Migration: custom legacy chord → preserved, defaults not added; old-default
  legacy → new default set; absent legacy → new default set; legacy keys not
  rewritten.
- `HotkeyConflictDetector`: new reserved entries; duplicate-binding detection;
  warning tier (Ctrl+Shift+Space, Ctrl+Alt+letter); existing tests keep
  passing.
- `KeyCodeMapper`: modifier-side mappings.

**Parlotype.Desktop.Tests (headless):**
- `HotkeySettingsViewModel`: add preset, add recorded chord, remove, persist →
  `HotkeyBindings` JSON; conflict/warning surfaced; per-chord mode change.
- `HotkeyCoordinator`: start/stop/cancel routing; `SetDictationActive` follows
  `RecordingState`; cancel discards (no injection call).
- `TranscribeViewModel.HotkeyHintText` derivation; `CancelRecordingAsync`
  resets state without transcription.
- Update `MockGlobalHotkeyService` to the new contract; screenshot test for the
  reworked settings page (`HotkeySettingsScreenshotTests`).

**Manual verification (Definition of Done #2):**
- Hold Right Ctrl to dictate into Notepad; Right Ctrl+C aborts within grace.
- Double-tap Ctrl toggles; Ctrl+C/Ctrl+V flurry does not.
- Ctrl+Alt+Space toggles; Esc cancels mid-recording (focused and unfocused);
  Esc still hides the idle widget; left-Ctrl shortcuts unaffected throughout.

## 11. Risks & open questions

1. **SharpHook left/right fidelity** — trackers depend on `VcRightControl` vs
   `VcLeftControl` arriving distinctly, and the AltGr filter depends on
   side-resolvable Alt state. Verify against SharpHook 7 on Windows early
   (spike before building the UI); ADR-020 notes the hook basics but not
   side-specific behaviour. Record findings in `memory/knowledge/`.
2. **Hold-abort vs. already-started audio** — the grace-window abort fires
   ~≤300 ms after capture started; the discard path must tolerate cancelling
   during `StartRecordingAsync`'s in-flight await (ADR-039 interaction).
3. **Escape suppression scope** — suppressing Esc while recording steals it
   from the foreground app (e.g. closing an IntelliSense popup). Acceptable
   trade-off (recording is a deliberate, visible mode) but worth an ADR note.
4. **Migration judgment call** (§6) — "custom chord users don't get new
   defaults" is the conservative choice; flag in the PR description for review.
5. **macOS/Wayland** — out of scope; the research constraints (TCC prompts,
   `GlobalShortcuts` portal, no Fn capture) are recorded in
   [research.md](research.md) for when those platforms ship. The Core model
   already accommodates a chord-only degraded mode.
