---
title: "Session: 2026-07-30"
type: session
status: active
tags: [hotkeys, core, platform, desktop, adr-047]
created: 2026-07-30
summary: "Planned and shipped multi-binding dictation hotkeys (ADR-047): Hold Right Ctrl PTT, Double-tap Ctrl toggle, Ctrl+Alt+Space chord, Esc cancel, per-binding modes, two-tier conflict validation, record-button tooltip."
---

# Session: 2026-07-30

## Active Focus
- Plan [2026-07-30-dictation-hotkey-bindings](../../plans/2026-07-30-dictation-hotkey-bindings/) — created from user-supplied competitive research and completed the same session. [ADR-047](../../docs/decisions/047-multi-binding-dictation-hotkeys.md).
- Rebuilt the hotkey subsystem from "one chord + one global mode" to a *list* of gestures: 12 new Core files, `SharpHookHotkeyService` reduced to an adapter, `HotkeySettingsView` rebuilt as a binding list, cancel path added to `TranscribeViewModel`.
- Build clean (0 warnings from touched files), 1003 tests green (was 915), plus an end-to-end harness driving a real global hook.

## Decisions Made
- **Deferred hold-start (the non-obvious one).** The recommended defaults bind hold *and* double-tap to Ctrl. Starting the hold on key-down made every deliberate double-tap emit Start/Cancel/Start/Cancel/Start — visible flicker. When a double-tap binding shares a hold binding's physical key, the hold's start now waits out the 250 ms tap window; releasing earlier emits nothing at all. Costs 250 ms latency + leading audio in the default config; a non-overlapping hold (e.g. Right Alt) still starts instantly.
- **Bare modifiers are never suppressed** — swallowing a Ctrl key-down would break every Ctrl shortcut system-wide. Only chords (both edges) and a cancelling Escape suppress.
- **`SetDictationActive(bool)` replaced the service's private toggle-state guess**, which desynced whenever recording started/stopped via the widget button or failed on `CloudProviderNotConfiguredException`. `HotkeyCoordinator` observes `RecordingState` and pushes the truth down; `Loading` counts as active so Escape can abandon a pending start.
- **Cancel needs no Core audio contract change** — `CancelRecordingAsync` detaches `TranscriptionAvailable` *before* stopping the pipeline, so anything still produced has nowhere to go.
- **Migration is conservative**: a legacy chord differing from the old `Ctrl+Shift+Space` default becomes the user's *only* binding (no surprise global hotkeys on upgrade); absent/default legacy yields the full new default set.
- **Persistence as a readable string list** (`hold|Ctrl|Right|PushToTalk`) rather than serialized JSON objects — keeps settings.json legible and lets unparseable entries be dropped instead of failing the load.
- Retired `Ctrl+Shift+Space` as a default (Parameter Info in VS / signature help in VS Code) — it survives as a *warning*-tier binding users may still choose.

## Facts Learned
- SharpHook's `EventMask` **is side-resolvable** (`LeftCtrl=0x02`, `RightCtrl=0x20`), and the unqualified `EventMask.Ctrl=0x22` is the OR of both — so `HasFlag(EventMask.Ctrl)` is wrong (demands both keys) while `(mask & EventMask.Ctrl) != 0` is right. Captured in [[sharphook-modifier-sides]] along with the distinct `VcLeftControl`/`VcRightControl` codes the whole design rests on.
- `EventSimulator`-injected keys **are** visible to a `SimpleGlobalHook`, which made it practical to verify the Platform adapter end-to-end from an automated harness rather than by hand. They carry `EventMask.SimulatedEvent`, which is `0x0000` — visible in `ToString()` but not testable bitwise.
- The abort grace window interacts with the deferred start: at human typing speed a Right Ctrl+C abort lands *before* the 250 ms deferred start, so no recording is created at all — better than starting and discarding. A slow (>300 ms) Ctrl+C leaves a short recording that stops normally and captures silence, so nothing is injected.
- `TranscribeWindow`'s Escape previously hid the widget **without stopping recording**, and `StopRecordingAsync` always transcribed+injected — there was no discard path anywhere in the pipeline before this session.

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/047-multi-binding-dictation-hotkeys.md`.
- Vault (services/architecture): done — `memory/architecture/subsystems.md` (Global Hotkeys section rewritten), `memory/services/core.md`, `platform.md`, `desktop.md`, `tests.md`, `memory/decisions/_index.md`.
- Knowledge (non-derivable facts): done — `memory/knowledge/sharphook-modifier-sides.md` + index row.

## Manual Verification (user, 2026-07-30)
All gestures confirmed working in the real app: hold Right Ctrl, hold Right Alt, double-tap Ctrl, chord recording in settings, the record-button tooltip, and Escape. Double-tap mattered most to confirm live — it is the one path the end-to-end harness could not fully exercise, because there the `SetDictationActive` feedback was supplied by hand rather than by the coordinator observing `RecordingState`. Its working confirms that loop end to end.

## Next Action
- Work is complete and verified but **uncommitted** on branch `claude/dictation-hotkeys-research-512c59` — commit and open a PR when the user asks.
- Deferred by scope, worth revisiting if users complain: the 250 ms push-to-talk delay in the default configuration could be removed by splitting "start capture" from "start dictation session" in `TranscribeViewModel`, letting audio buffer at t=0 while the UI waits out the tap window.
- Unrelated pre-existing warnings noticed but not touched: `AVLN5001` obsolete-API in `ModelDownloadDialog.axaml` (`SystemDecorations`) and `LlamaCppSettingsView.axaml` (`TextBox.Watermark`).
- **Pre-existing flaky tests, confirmed not ours.** `WhisperRuntimeFallbackTests.LoadedRuntime_IsNull_BeforeAnyFactoryCreation` and `WhisperRuntimeBootstrapTests.EnsureInitializedAsync_MissingSetting_DefaultsToAuto` fail roughly 1 run in 3. They assert on Whisper's process-global one-shot runtime selection (ADR-012/022), so whichever test reaches it first in a parallel run decides the outcome. Verified reproducing on a clean worktree at 940ca3e with none of this session's changes. Fix would be an xUnit collection to serialize them; flagged as a separate task.
