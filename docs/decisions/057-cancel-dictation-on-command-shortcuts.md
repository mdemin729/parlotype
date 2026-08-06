---
status: accepted
date: 2026-08-04
---

# 057. Cancel Dictation on Command Shortcuts, and Make Cancel a Real Abort

## Context

[ADR-047](047-multi-binding-dictation-hotkeys.md) ships **Hold Right Ctrl** as the
default push-to-talk gesture. `ModifierHoldTracker` treats a non-modifier keypress as
evidence that the user reached for a shortcut rather than a dictation — but only inside
a 300 ms grace window (`HotkeyGestureTiming.HoldAbortGraceMs`), because the same tracker
is meant to tolerate typing during dictation. ADR-047 listed the fallout itself:

> The abort grace window (300 ms) only covers shortcuts typed at normal speed. A slow
> Right Ctrl+C leaves a short recording that stops normally.

It claimed such a recording "captures silence, so nothing is injected". That is only true
when the user is not speaking. Hitting Ctrl+C mid-sentence — reaching for the shortcut
without hurrying — transcribes the speech captured so far and types it into whatever the
user was working in.

The premise the grace window rests on does not hold for Ctrl or Alt. **Nothing typed
while Ctrl or Alt is down arrives at the target application as text.** Every keystroke
during a Ctrl hold is a command, so "users do type while dictating" cannot describe this
gesture; it describes Shift, and toggle-mode dictation where no modifier is held at all.

Separately, cancel was never a true discard. `TranscribeViewModel.CancelRecordingAsync`
detaches `TranscriptionAvailable` and then calls `IAudioPipeline.StopAsync`, which
*drains*: the segmenter runs its final VAD flush, the transcription loop calls the
recognizer with a hardcoded `CancellationToken.None`, and the drain is allowed 30 seconds.
Nothing is typed, but the machine pays full inference on audio the user has abandoned, and
since `StartAsync` no-ops while `IsRunning`, a cancel followed quickly by a new dictation
could be swallowed.

## Decision

### 1. A Ctrl or Alt hold aborts on any keystroke, whenever it lands

`ModifierHoldTracker` drops the timing term when the held modifier is a *command*
modifier:

```csharp
private bool IsCommandModifierHold =>
    _gesture.Modifier is ModifierKey.Ctrl or ModifierKey.Alt;
```

Shift and Meta holds keep the 300 ms window. Shift composes text, so typing under it is
genuinely ambiguous; Meta is not a shipped gesture, and Win+key is rare enough that the
existing behaviour is not worth changing on speculation.

Nothing downstream changes: the branch already sets `_aborted`, returns
`HoldOutcome.Aborted`, and reaches `TranscribeViewModel.CancelRecordingAsync` through
`DictationAction.Cancel`. The abort stays latched until key-up, so releasing Right Ctrl
after a Ctrl+C emits nothing rather than a `Stop`.

**Suppression is unchanged**, and that is the point of the design: bare modifiers are
never suppressed (ADR-047 §5) and a letter is not a chord match, so the target application
still receives a working `Ctrl+C`. The user gets the shortcut they asked for *and* loses
the recording they did not.

### 2. Scope is hold gestures, not all dictation

The rule could have been "any keystroke with Ctrl or Alt held cancels, whatever started
the recording". It is not. During toggle-mode dictation the modifier is genuinely free,
and a user saving with Ctrl+S mid-sentence wants the file saved, not the dictation
destroyed. Only a hold — where the user's own hand is holding the modifier down — carries
the intent.

### 3. Cancel discards instead of draining

`IAudioPipeline` gains `CancelAsync`. `AudioPipelineService` implements both stops through
a shared `ShutdownAsync(bool discard, ct)`:

- A `CancellationTokenSource` created in `StartAsync` is passed to the transcription loop
  and cancelled by a discard, which replaces the hardcoded `CancellationToken.None`. An
  ordinary stop never fires it, so drain-on-stop still completes in-flight work.
- A `_discarding` flag makes `FlushBuffer` skip the final VAD pass and makes the
  transcription loop drain the utterance channel without recognizing anything.
- `OperationCanceledException` is caught ahead of the generic handler and does **not**
  raise `TranscriptionFailed` — a user cancel is not an error, and that event puts a
  dialog on screen.
- The drain wait drops from 30 s to 5 s for a discard, since nothing should still be
  running.
- `StartAsync` and `ShutdownAsync` (both `StopAsync` and `CancelAsync`) now serialize
  behind a new `_lifecycleLock`, mirroring the existing `_initLock` pattern in the same
  class. Nothing upstream guarantees a stop/cancel and a start never overlap —
  `TranscribeViewModel.CancelRecordingAsync` deliberately does not wait on an in-flight
  start (ADR-039) — and the two shutdown entry points this ADR adds double the call sites
  that could race each other. Without the lock, two overlapping shutdown calls could both
  pass the `IsRunning` gate, or a stale call's cleanup tail — nulling
  `_segmenterTask`/`_rawChannel`/etc. and flipping `IsRunning` false — could land on a
  session a concurrent `StartAsync` had already begun, silently killing tracking of a live
  recording. The lock costs nothing on the normal sequential path — a caller already
  awaits `StopAsync`/`CancelAsync` before issuing a new `StartAsync` in every current call
  site — and turns the racy overlap into deterministic queuing.

`TranscribeViewModel` calls `CancelAsync` from both discard paths —
`CancelRecordingAsync` and `DiscardStartedRecordingAsync` (the recording that only exists
because a model load finished after the user gave up on it). `DetachPipelineHandlers`
stays, and stays *first*: it covers the utterance that completed between the keystroke and
the cancel arriving.

The interface member has no default implementation. Delegating to `StopAsync` would
silently transcribe, which is the bug being fixed.

## Consequences

**Easier:**

- A shortcut typed at any speed during a Right Ctrl hold discards the recording. The
  window in which a mis-timed Ctrl+C could type a sentence into the user's editor is gone.
- Cancelling stops paying for the transcription. Previously an abandoned recording ran
  Whisper to completion in the background.
- Cancel returns promptly instead of waiting out a drain, so a cancel followed
  immediately by a new dictation is no longer at risk of hitting a still-`IsRunning`
  pipeline.

**More difficult:**

- `IAudioPipeline` is a breaking change; every implementation and mock needs `CancelAsync`.
- The pipeline now carries discard state (`_discarding`, `_transcribeCts`) read across
  three threads, where before shutdown was expressed purely by completing channels. A
  code review of this change caught two consequences of that: a decode outliving the
  drain timeout could publish into a session that restarted in the meantime (fixed by the
  session-scoped `cancellationToken` recheck above), and nothing serialized `StartAsync`
  against `ShutdownAsync`, so two overlapping calls — the two shutdown entry points this
  ADR adds double the sites that could race — could both pass the `IsRunning` gate, or a
  stale call's cleanup tail could null out a session a concurrent `StartAsync` had already
  begun (fixed by `_lifecycleLock`, §"Decision" above).
- sherpa-onnx cannot interrupt a decode already inside native code
  (`ParakeetSpeechRecognizer` notes this), so a cancel may still pay out the tail of one
  short utterance. It is discarded either way — `TranscribeLoopAsync` rechecks its own
  session-scoped `cancellationToken` right before firing `TranscriptionAvailable`, so a
  decode that outlives the 5 s drain wait and returns after a brand-new session has
  already started still cannot publish into it. The cancel just isn't instant. The CTS is
  left undisposed if the drain times out, since the loop observing the token is still
  running.
- Two behaviours now diverge by modifier — Ctrl/Alt versus Shift/Meta — which is one more
  rule to hold in mind when reading the tracker.
- A user who deliberately wants to press a Ctrl shortcut *while* dictating push-to-talk
  cannot: the recording ends. This is accepted; the gesture makes such a keystroke
  indistinguishable from an accident, and Escape or simply releasing the key were never
  the problem.

**Verified:** `A_Slow_Right_Ctrl_Shortcut_Still_Cancels_The_Recording` and
`An_Alt_Hold_Cancels_On_Any_Key` cover the gesture at the matcher level, with
`Typing_Well_Into_A_Shift_Hold_Does_Not_Abort_It` holding the old behaviour in place for
Shift. `CancelAsync_DiscardsBufferedSpeech_WithoutTranscribing` and
`CancelAsync_AbortsATranscriptionAlreadyInFlight` (a recognizer that blocks until its
token fires) cover the pipeline. The two review-driven fixes each have a dedicated
regression test, both confirmed to fail without their fix before being verified green:
`CancelAsync_DiscardsADecodeThatOutlivesTheDrainTimeout_EvenAcrossARestart` (a recognizer
that ignores its cancellation token entirely, standing in for an uninterruptible native
decode, released only after a second session has already started) and
`StartAsync_WaitsForAConcurrentShutdownToFinish` (a capture double whose `StopAsync` hangs
until released, proving a concurrent `StartAsync` queues instead of racing). Full suite
green at 1126 tests.
