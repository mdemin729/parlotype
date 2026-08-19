---
status: accepted
date: 2026-08-18
---

# 060. Hold-Scoped Push-to-Talk, and Per-Engine Utterance Ceilings

## Context

`AudioPipelineService.ProcessBatch` ends an utterance when trailing silence exceeds the
user's **Silence timeout** setting (`WaitTimeOption`, 0.5–3 s). The utterance is
transcribed and injected immediately, and the buffer is cleared.

That rule is a *guess* at when the speaker finished. In push-to-talk there is no need to
guess: releasing the key says so exactly. Layering a heuristic on top of an explicit
signal can only be wrong, and when it is wrong it is wrong in an expensive way — the
sentence is cut mid-flow, so:

- the words either side of the cut are corrupted, because each fragment is decoded
  without the other's context;
- punctuation is decided on a fragment, producing a full stop where a comma belonged;
- the fragment is injected into the user's editor immediately, so the correction is
  manual.

Measured on a synthetic pause-controlled sample, the corruption is not subtle — a cut
through "this evening" produced **"V7"**:

```
flush 500 ms   ... What your plans for V7. ... Yeah.
flush 3000 ms  ... What'd your plans for this evening? ... Bye-bye.
one-shot       ... What'd your plans for this evening? ... Bye-bye.
REFERENCE      ... What are your plans for this evening? ... Goodbye.
```

The longest available setting (Very Long, 3 s) does not fix it: a user who pauses longer
than 3 s mid-sentence has no setting left to reach for.

The obvious fix — never flush mid-recording — turned out to be unsafe past a point, and
the point is engine-specific. Full data in
`plans/2026-08-18-hold-scoped-push-to-talk/research.md`; the two findings that constrain
this decision:

**Parakeet silently loses text as a single decode grows.** Word retention against a known
reference: 100 % at 60 s, **60 % at 120 s, 48 % at 300 s**. CER tracks WER almost exactly,
the signature of dropped rather than misrecognised text.

**Parakeet then crashes outright above 400 s** — an `SEHException` out of native ONNX
Runtime, where a 5000-frame positional-encoding buffer (80 ms/frame → 400.0 s) runs out.
Today's 30 s force-flush is the only reason the app never reaches it.

**Whisper shows neither problem** (99 % retention, flat WER at 600 s) because whisper.cpp
chunks internally at 30 s.

## Decision

### 1. A new `PipelineMode.SingleUtterance`

Silence never ends the utterance; only the explicit stop does. `ProcessSingleUtterance`
runs the same incremental VAD as batch mode — extracted into a shared
`RunIncrementalVad` — but omits the silence-after-speech check entirely. The existing
stop-time `FlushBuffer` already does the right thing: one VAD pass over the whole buffer,
one extraction, one decode.

**VAD is retained, not bypassed.** The original request was to "ignore the VAD", but two
separable jobs were bundled in it. Deciding *segment boundaries* is what hurts; *filtering
audio* — trimming leading and trailing silence, dropping dead air, joining segments with
`InterSegmentSilenceMs` — is pure benefit. Handing an engine raw hold audio buys nothing
and costs decode time proportional to the silence in it.

### 2. Hold-scoped gestures select the mode; toggle gestures do not

The mode is not a user setting. A gesture that ends on key release carries its own
end-of-utterance signal; a toggle gesture does not, and a user who toggles dictation on
and talks for minutes still needs silence to break up the text.

`HotkeyMatchResult` gains `HoldScoped`, set for a `Start` produced by a hold tracker or by
a chord bound to `ActivationMode.PushToTalk`. It travels
`IGlobalHotkeyService.DictationStartRequested` (now
`EventHandler<DictationStartEventArgs>`) → `HotkeyCoordinator` →
`TranscribeViewModel.StartRecordingAsync(bool holdScoped)` → `IAudioPipeline.StartAsync`.
Nothing downstream can re-derive it, which is why it is plumbed rather than inferred.

The widget's record button leaves it false — it has no release to wait for.

### 3. `SpeechEngineLimits`: the ceiling is per-engine and mostly about correctness

| Engine | Ceiling | Basis |
|---|---|---|
| Parakeet | 60 s | Last rung with full word retention; well clear of the 400 s crash |
| Whisper | 300 s | Latency choice — no quality knee was found |
| Gemma 4, OpenAI-compatible, xAI Grok | 60 s | Unmeasured; takes the conservative value |

For Parakeet this is **not** a cost guard. Exceeding it does not make transcription slow,
it makes transcription *wrong*, silently. That is why the ceiling sits at the last clean
measurement rather than near the failure point.

The unmeasured engines are never worse off than before: every engine was previously
chopped at the pipeline's flat 30 s cap, so 60 s strictly increases the context they get.

**The ceiling measures speech, not elapsed time.** These limits were measured against
decode *input*, and `SpeechSegmentExtractor` discards everything between the segments, so
the recognizer never receives the pauses. Checking the raw buffer instead would split a
hold made mostly of thinking pauses — 10 s of speech either side of a 45 s pause is 65 s
held but only ~20 s decoded — for no reason at all, reintroducing the mid-sentence cut
this mode exists to remove, and precisely for the users who pause most.

A separate raw-buffer backstop (10 minutes) bounds memory for a hold that never ends — a
missed key-up, a lost focus event — since a mostly-silent hold can otherwise sit under the
speech ceiling indefinitely while the buffer grows at 64 KB/s.

The engine is read *after* the recognizer initializes, not before.
`SpeechRecognizerFactory` re-reads `SettingsKeys.SpeechEngine` independently, and the
engine can be switched while a start is in flight (the settings view model only blocks
while `IsRecording`, which is not yet true during the model load). Reading last means the
only possible disagreement is a conservative one — a ceiling lower than the engine could
take — rather than handing Parakeet a Whisper-sized 300 s utterance.

### 4. Over the ceiling, split on a speech boundary

`FlushAtSpeechBoundary` flushes every *completed* speech segment and retains the final
one, which may still be mid-word, as the head of the next utterance. The cut therefore
lands in a pause.

The batch-mode overflow path is fixed alongside it. It queued `_sampleBuffer.ToArray()` —
the **raw** buffer, skipping the VAD extraction every other path applies — and now
extracts like everything else. It is visible in the data: at 300 s, flush-1000 scores
4.8 % WER with 100 % word retention while flush-3000 scores 16.2 % with 89 %, purely
because the latter hits the 30 s cap more often.

When one unbroken speech run exceeds the ceiling — the user has not paused once — there
is no boundary to use, so the cut lands mid-speech at exactly the ceiling and is logged as
a warning. The overrun past the cut is **retained**, not discarded: VAD only runs once
enough new samples have accumulated, so the tail it has not scanned yet is live speech the
user is still producing.

### 5. The Silence timeout setting stays, with honest copy

The setting still governs toggle-mode dictation, so it is not removed. Its description
claimed it "Applies to all speech engines", which is now false in the mode most users
dictate in; it now says push-to-talk ignores it and why.

## Consequences

**Easier:**

- A push-to-talk hold is transcribed as one utterance with full context. Sentence-length
  holds no longer split mid-flow, and punctuation is decided on the whole sentence.
- The Silence timeout setting stops being load-bearing for push-to-talk users, who
  previously had to max it out and still lost sentences.
- Fewer, larger decodes are cheaper than many small ones — RTF improved from 0.033 at a
  500 ms flush to 0.019 at 3000 ms on the same audio.
- Parakeet can no longer be handed audio past its crash point, whatever the ceiling is
  set to.

**More difficult:**

- **Latency moves to the end of the hold.** Text no longer trickles in during pauses;
  nothing appears until release. Parakeet's RTF (~0.02–0.05) keeps a 60 s hold under ~3 s,
  but the widget's "Transcribing…" state now matters more than it did.
- Holds past the ceiling still split, so the seam problem is reduced, not eliminated. A
  user who dictates for minutes without pausing gets a mid-speech cut.
- Two segmentation paths now exist in `AudioPipelineService`. They share
  `RunIncrementalVad`, but batch and single-utterance behaviour must be reasoned about
  separately.
- The ceilings for Gemma 4 and the two cloud engines are placeholders standing on
  Parakeet's measurement. Neither cloud recognizer has a client-side size guard either;
  16 kHz mono 16-bit WAV runs 32 KB/s, so OpenAI's 25 MB request limit lands near 13
  minutes — far above the 60 s ceiling, but unenforced if that ceiling ever rises.

## Alternatives considered

**Make it a user setting.** Rejected: the correct behaviour is derivable from the gesture,
and a setting would ask users to understand the difference between a hold and a toggle in
order to get sensible defaults.

**Raise the Silence timeout range instead.** Rejected: it only moves the cliff. Any finite
timeout cuts a long enough pause, and the reporter had already exhausted the range.

**Drop the VAD entirely for push-to-talk, as originally proposed.** Rejected: see §1. VAD's
filtering role is separable from its segmenting role and is worth keeping.

**Set the Parakeet ceiling near the 400 s crash point.** Rejected: quality collapses from
~120 s. A ceiling at 300 s would keep the app from crashing while letting it silently drop
half the user's words, which is worse than crashing.

## References

- `plans/2026-08-18-hold-scoped-push-to-talk/research.md` — benchmark method and data
- [ADR-041](041-parakeet-v3-sherpa-onnx.md) — Parakeet via sherpa-onnx
- [ADR-047](047-multi-binding-dictation-hotkeys.md) — gesture model and `ActivationMode`
- [ADR-057](057-cancel-dictation-on-command-shortcuts.md) — `CancelAsync` and the discard path
