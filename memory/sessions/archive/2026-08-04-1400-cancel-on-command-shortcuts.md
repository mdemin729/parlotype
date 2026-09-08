---
title: "Session: 2026-08-04 — Cancel dictation on Ctrl/Alt shortcuts"
type: session
status: complete
tags: [hotkeys, audio-pipeline, adr-057]
created: 2026-08-04
summary: "Ctrl/Alt holds now abort on any keystroke (not just inside the 300 ms grace window), and cancel became a real pipeline discard via new IAudioPipeline.CancelAsync"
---

# Session: 2026-08-04

## Active Focus

- `src/Parlotype.Core/Hotkeys/ModifierHoldTracker.cs` — `IsCommandModifierHold`
- `src/Parlotype.Core/Audio/IAudioPipeline.cs` — new `CancelAsync`
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — `ShutdownAsync(discard, ct)`,
  `_transcribeCts`, `_discarding`
- `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs` — both discard paths
- Tests across `Parlotype.Tests` + `Parlotype.Desktop.Tests`

## Decisions Made

- **Ctrl/Alt holds abort on any keystroke, at any time.** The grace window's premise
  ("users type while dictating") cannot hold for a command modifier — nothing typed under
  Ctrl or Alt reaches the target app as text. Shift (composes text) and Meta (not a shipped
  gesture) keep the 300 ms window. See [[decisions/_index|ADR-057]].
- **Scope is holds only.** Rejected the broader "any Ctrl/Alt keystroke while dictating
  cancels": in toggle mode the modifier is genuinely free, and `Ctrl+S` mid-dictation should
  save the file, not destroy the recording.
- **Cancel discards instead of draining.** New `IAudioPipeline.CancelAsync` with no default
  implementation — delegating to `StopAsync` would silently transcribe, which is the bug.
- **Suppression untouched**, so the user gets the shortcut *and* loses the recording. That
  duality is the whole design.

## Facts Learned

- ADR-047 recorded this exact gap in its Consequences and dismissed it: a slow
  `Right Ctrl+C` "captures silence, so nothing is injected". That only holds when the user
  isn't speaking — mid-sentence it transcribed and typed. Worth remembering that a
  documented-and-accepted consequence isn't the same as a verified one.
- The cancel path was never a true discard. `DetachPipelineHandlers()` stopped the *text*,
  not the *work*: `StopAsync` drained, flushed, and called the recognizer with a hardcoded
  `CancellationToken.None` under a 30 s budget.
- Every recognizer already honours the token end-to-end (`WhisperSpeechRecognizer` →
  `_processor.ProcessAsync`, cloud → `HttpClient`, Parakeet throws on a pre-cancelled
  token). Only the pipeline was severing it.
- `ParakeetSpeechRecognizer` documents that sherpa-onnx cannot cancel mid-decode, so a
  discard may still pay out one short utterance's tail.
- Two existing tests were exact statements of the old intent and had to be inverted:
  `ModifierHoldTrackerTests.Key_Pressed_After_Grace_Window_Does_Not_Abort` and
  `HotkeyGestureMatcherTests.Typing_Well_Into_A_Hold_Does_Not_Abort_It`. Both were rewritten
  with a Shift-hold sibling preserving the original rule where it still applies.

## Open Blockers

- None technically. Manual end-to-end verification (hold Right Ctrl, speak, press `C`)
  needs a physical keyboard and microphone and was not performed in this session — the
  behaviour is covered by unit tests at the matcher and pipeline levels only.

## Code review follow-up

A review of the diff (agent-generated, findings verified independently against the code
before acting) caught two real concurrency bugs in the new discard path, both fixed:

1. **Zombie decode after restart.** sherpa-onnx can't observe cancellation mid-decode. If a
   call already in flight when `CancelAsync` fires outlives the 5 s drain timeout, it
   returns normally instead of throwing — and without a recheck, that stale result could
   publish `TranscriptionAvailable` into a session that had since restarted and attached a
   fresh handler. Fixed: `TranscribeLoopAsync` now rechecks its own session-scoped
   `cancellationToken` (bound to *that* invocation's CTS via closure, immune to the field
   being reassigned by a new `StartAsync`) right before firing the event. Regression test
   `CancelAsync_DiscardsADecodeThatOutlivesTheDrainTimeout_EvenAcrossARestart` — confirmed
   to fail without the fix before being verified green.
2. **No lifecycle serialization.** Nothing prevented two overlapping `StartAsync`/
   `ShutdownAsync` calls — the view model's cancel path deliberately doesn't wait on an
   in-flight start (ADR-039), and the two new shutdown entry points doubled the racing
   surface. Fixed with a `_lifecycleLock` (`SemaphoreSlim(1,1)`, same pattern as the
   existing `_initLock`) wrapping both methods. Regression test
   `StartAsync_WaitsForAConcurrentShutdownToFinish` — also confirmed to fail (fast, clean
   assertion failure, not a hang) without the fix.

The review's specific trigger example for finding 2 ("focused Escape through both the
global hook and window handler") doesn't actually hold — `SharpHookHotkeyService` sets
`e.SuppressEvent = true` synchronously on the hook thread before the OS ever queues the
keystroke to Parlotype's own window, so the two Escape paths (`TranscribeWindow.OnKeyDown`
vs. the global hook) are mutually exclusive for a single physical keypress, matching the
code comment there. The underlying race is still real via other paths (e.g. an overlapping
button-click Stop and hotkey Cancel interleaving through `async`/await re-entrancy on the
UI thread), so the fix was applied anyway.

**Process note:** writing the negative-control test for finding 2 first hung the whole test
run — not a production bug, but a bug in the *test*: `Assert.False` threw before
`capture.ReleaseStop()` ran, and the pipeline's `await using` disposal then deadlocked
retrying the same never-released gate. Fixed by wrapping the risky assertions in
`try/finally { capture.ReleaseStop(); }`. Worth remembering for any future test built on a
manually-gated fake: release the gate unconditionally, or a failing assertion hangs CI
instead of failing it.

## Documentation Status

- ADR: done — `docs/decisions/057-cancel-dictation-on-command-shortcuts.md`; ADR-047 §7 and
  its Consequences bullet annotated as amended/superseded
- Vault (services/architecture): done — `services/core.md`, `services/platform.md`,
  `services/desktop.md`, `architecture/subsystems.md`, `architecture/audio-pipeline.md`,
  `decisions/_index.md`
- Knowledge (non-derivable facts): none — everything learned is now derivable from the code
  and ADR-057

## Next Action

Manual pass on a real keyboard: hold Right Ctrl, speak a sentence, wait ~2 s, press `C`
while still holding. Expect the widget to leave the recording state, "Cancelled" status,
no injected text, and a working clipboard copy. Then confirm a normal release still
injects, and that `Ctrl+S` during double-tap toggle dictation does *not* cancel.
