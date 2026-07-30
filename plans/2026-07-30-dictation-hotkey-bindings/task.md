---
title: Multi-binding dictation hotkeys (hold, double-tap, chord, cancel)
status: completed
created: 2026-07-30
started: 2026-07-30
completed: 2026-07-30
---

# Multi-binding dictation hotkeys

## Problem

Parlotype today supports exactly **one** hotkey binding, and it must be a chord
(≥1 modifier + a non-modifier key — `HotkeyBinding.IsValid`). The default is
`Ctrl+Shift+Space`, which collides with *Parameter Info* in Visual Studio and
signature help in VS Code — a bad default for a developer-heavy audience.
`ActivationMode` (PTT vs Toggle) is a single global switch, so users cannot have
push-to-talk *and* hands-free toggle at the same time. There is no way to bind
"hold a bare modifier" (the Wispr Flow model) or "double-tap a modifier" (Apple
Dictation's own default), no cancel gesture, and the recording widget gives no
hint of what the current hotkey is.

Competitive research (see [research.md](research.md)) converged on a concrete
feature set, which this plan implements:

1. **Two default bindings, not one** — dictation has two genuinely different modes.
2. **Push-to-talk (primary default):** `Hold Right Ctrl` — same physical key on
   every platform, no chord, doesn't disturb normal Ctrl shortcuts (left Ctrl).
3. **Toggle (secondary default):** `Double-tap Ctrl` — Apple's own convention on
   external keyboards; collides with nothing on Windows/Linux.
4. **Explicit chord fallback:** `Ctrl+Alt+Space` (replaces `Ctrl+Shift+Space`
   as the shipped chord).
5. **Cancel:** `Escape` while recording — discard audio, no transcription.
6. **Multiple bindings per action** (add/remove in settings).
7. **Validation** of new bindings against OS-reserved shortcuts *and* against
   the user's other Parlotype bindings.
8. **Tooltip on the recording indicator** showing the current push-to-talk key.

## Approach

Rework the hotkey subsystem from "one chord + one global mode" to "a set of
gesture bindings", keeping the Core/Platform split:

- **Core:** new `HotkeyGesture` model with three kinds — `Chord` (today's
  `HotkeyBinding`), `HoldModifier` (bare modifier, side-aware: Right Ctrl),
  `DoubleTapModifier` — plus a `DictationHotkey` record pairing a gesture with
  its `ActivationMode`. Pure, timestamp-driven tap/hold state machines live in
  Core so they are unit-testable without SharpHook.
- **Platform:** `SharpHookHotkeyService` feeds raw key events into the gesture
  matchers, resolves them to semantic events (start / stop / cancel), and
  persists the binding set as JSON under a new `SettingsKeys.HotkeyBindings`
  key with one-time migration from the legacy `HotkeyModifiers`/`HotkeyKey`/
  `ActivationMode` triple. **Never suppress bare-modifier events** (that would
  break every Ctrl shortcut system-wide); suppression stays chord-only.
  `Escape` is intercepted only while dictation is actually active.
- **Desktop:** Hotkey settings page becomes a binding list with preset "Add
  binding" options (Hold Right Ctrl / Double-tap Ctrl / record a chord…),
  inline conflict warnings, and per-chord mode choice. `HotkeyCoordinator`
  gains a cancel path (`TranscribeViewModel.CancelRecordingAsync` — discard,
  don't transcribe) and reports recording state back to the hotkey service so
  Escape/toggle also work for recordings started from the mic button.
  `TranscribeWindow`'s Esc handler changes to: recording → cancel; idle → hide
  to tray (current behaviour). The record button gets a tooltip like
  "Hold Right Ctrl to talk · Esc to cancel".

Full design, decision points, and risk notes: [implementation-plan.md](implementation-plan.md).
Scope note: v1 targets Windows (the only shipping platform today); the Core
model stays platform-agnostic and the macOS/Wayland constraints from research
are recorded for the future, not implemented now.

## Workplan

- [x] **Spike:** confirmed SharpHook 7.1.1 reports `VcLeftControl`/`VcRightControl` distinctly *and* that `EventMask` is side-resolvable (`LeftCtrl=0x02`, `RightCtrl=0x20`, `Ctrl=0x22` is their OR) — recorded in [[sharphook-modifier-sides]]
- [x] **Core model:** `HotkeyGesture` (Chord / HoldModifier / DoubleTapModifier), `ModifierKey` + `ModifierSide`, `DictationHotkey` (gesture + mode), validation matrix, `DictationHotkeyDefaults`; `HotkeyBinding` kept as the chord payload
- [x] **Core state machines:** `ModifierTapTracker` and `ModifierHoldTracker` — pure, timestamp-driven, thresholds in `HotkeyGestureTiming`; composed by `HotkeyGestureMatcher`, which emits `DictationAction` + suppression
- [x] **Conflict detection:** `HotkeyConflictDetector.Check(candidate, existing)` → `HotkeyConflict` with Blocking/Warning severity; reserved list gained Win+H, Win+Ctrl+S, Win+Ctrl+Space; warn tier for Ctrl+Shift+Space and `Ctrl+Alt+<letter>`
- [x] **Platform:** `SharpHookHotkeyService` reduced to an adapter over the matcher — side-aware codes, semantic events, chord-only suppression, state-gated Escape, `Timer` for deferred holds; `KeyCodeMapper` gained `ToModifier`/`IsRightAltHeld`
- [x] **Persistence + migration:** `SettingsKeys.HotkeyBindings` as a readable string list via `HotkeyBindingCodec`; `HotkeySettingsMigrator` preserves a user's custom chord alone and never rewrites the legacy keys
- [x] **Desktop coordinator:** semantic wiring, `TranscribeViewModel.CancelRecordingAsync` (detach-then-stop discard), `SetDictationActive` fed from `RecordingState`, hint republished on `BindingsChanged`
- [x] **Desktop UI:** binding-list settings page (preset menu + chord recorder, remove, per-chord mode toggle, two-tier warnings); `TranscribeWindow` Esc cancels while recording; record-button tooltip via `HotkeyHint`
- [x] **Tests:** 67 new Core tests + 21 new Desktop tests (1003 total, all green)
- [x] **Behaviour verified end-to-end:** an integration harness drove the real `SharpHookHotkeyService` against a real global hook with synthesized keys — all 8 scenarios pass (see ADR-047 "Verified")
- [x] **Docs:** [ADR-047](../../docs/decisions/047-multi-binding-dictation-hotkeys.md); vault updated (`memory/architecture/subsystems.md`, `memory/services/core|platform|desktop|tests.md`, `memory/decisions/_index.md`, new `memory/knowledge/sharphook-modifier-sides.md`)

## Outcome notes

Two design decisions emerged during implementation that the plan had not anticipated:

- **Deferred hold-start.** The recommended defaults bind hold *and* double-tap to
  Ctrl, so starting the hold on key-down made every deliberate double-tap emit
  Start/Cancel/Start/Cancel/Start — a visible flicker. When a double-tap binding
  shares a hold binding's key, the hold's start now waits out the 250 ms tap
  window. Cost: 250 ms of latency and leading audio in the default configuration.
- **`SetDictationActive`** replaced the service's private toggle-state guess,
  which desynced whenever recording started or stopped by other means (the
  widget's button, a failed model load). The coordinator owns the truth now.
