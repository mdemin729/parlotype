---
title: Cancel dictation on Ctrl/Alt shortcuts, and make cancel a real abort
status: completed
created: 2026-08-04
started: 2026-08-04
completed: 2026-08-04
---

# Cancel dictation on command shortcuts

## Problem

With the shipped default (Hold Right Ctrl → push-to-talk, [ADR-047](../../docs/decisions/047-multi-binding-dictation-hotkeys.md)),
pressing `Ctrl+C` more than 300 ms into a hold does **not** discard the recording.
`ModifierHoldTracker` only aborts inside `HotkeyGestureTiming.HoldAbortGraceMs`, on the
premise that "users do type while dictating". ADR-047 recorded the gap and dismissed it —
such a recording "captures silence, so nothing is injected" — but that only holds when the
user is not speaking. Reaching for the shortcut mid-sentence transcribes the speech so far
and types it into the user's editor.

The premise is wrong for Ctrl and Alt specifically: nothing typed under them reaches the
target app as text, so every keystroke during such a hold is a command.

Second problem, surfaced by the same report: cancel never really cancelled. It detached
`TranscriptionAvailable` and then called `StopAsync`, which drains — final VAD flush,
recognizer called with `CancellationToken.None`, 30 s drain budget. No text was typed, but
the inference ran to completion.

## Approach

1. **Core gesture** — `ModifierHoldTracker` drops the timing term for Ctrl/Alt holds
   (`IsCommandModifierHold`). Shift and Meta keep the grace window. Suppression untouched,
   so the target app still gets its `Ctrl+C`.
2. **Scope** — hold gestures only. Toggle-mode dictation is unaffected: `Ctrl+S` while
   hands-free dictating saves the file and keeps recording.
3. **Core contract** — new `IAudioPipeline.CancelAsync`, no default implementation.
4. **Platform** — `AudioPipelineService.ShutdownAsync(discard, ct)` behind both
   `StopAsync` and `CancelAsync`; `_transcribeCts` replaces the hardcoded
   `CancellationToken.None`; `_discarding` skips the final flush and the recognizer;
   `OperationCanceledException` no longer raises `TranscriptionFailed`; 5 s drain budget
   for a discard.
5. **Desktop** — both view-model discard paths call `CancelAsync`.

## Workplan

- [x] `ModifierHoldTracker.IsCommandModifierHold`; `HotkeyGestureTiming` doc scope
- [x] `IAudioPipeline.CancelAsync` + `AudioPipelineService.ShutdownAsync(discard)`
- [x] `TranscribeViewModel.CancelRecordingAsync` / `DiscardStartedRecordingAsync` → `CancelAsync`
- [x] `MockAudioPipeline.CancelAsync` + `CancelCount`
- [x] Tests: inverted the two that encoded the old grace-window behaviour, added Alt and
      Shift hold coverage, switched `HotkeyCancelTests` to `CancelCount`, added two
      `AudioPipelineService` cancel tests (1124 total, all green)
- [x] [ADR-057](../../docs/decisions/057-cancel-dictation-on-command-shortcuts.md); ADR-047
      §7 and its Consequences bullet annotated as superseded
- [x] Vault: `memory/services/core|platform|desktop.md`, `memory/architecture/subsystems.md`,
      `memory/decisions/_index.md`
- [ ] Manual end-to-end pass on a real keyboard (needs a physical Right Ctrl + microphone)

## Code review follow-up

Two concurrency bugs were caught by review and fixed (see ADR-057 §Decision/Consequences):
`TranscribeLoopAsync` now rechecks its own session-scoped `cancellationToken` before
publishing (closes a window where a decode outliving the drain timeout could leak into a
restarted session), and `StartAsync`/`ShutdownAsync` now serialize behind a new
`_lifecycleLock`. Both have dedicated regression tests, each confirmed to fail without its
fix before being verified green. 1126 tests total.

## Outcome notes

The two tests that had to be inverted —
`ModifierHoldTrackerTests.Key_Pressed_After_Grace_Window_Does_Not_Abort` and
`HotkeyGestureMatcherTests.Typing_Well_Into_A_Hold_Does_Not_Abort_It` — were the clearest
statement of the old intent, so both were rewritten to assert the new rule with a
Shift-hold sibling preserving the original one. The "users type while dictating" case is
real; it just never applied to a Ctrl hold.
