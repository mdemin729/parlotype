---
status: accepted
date: 2026-07-30
---

# 047. Multi-Binding Dictation Hotkeys (Hold, Double-Tap, Chord, Cancel)

## Context

Parlotype supported exactly one hotkey, and it had to be a chord: `HotkeyBinding`
required at least one modifier plus a non-modifier key. `ActivationMode` was a
single global switch on `IGlobalHotkeyService`, so push-to-talk and toggle could
not coexist. The shipped default was `Ctrl+Shift+Space`.

Competitive research into how dictation tools actually bind keys (recorded in
`plans/2026-07-30-dictation-hotkey-bindings/research.md`) surfaced three problems:

1. **The default collides with the target audience's tools.** `Ctrl+Shift+Space`
   is Parameter Info in Visual Studio and signature help in VS Code.
2. **Dictation has two genuinely different modes.** Holding a key for one
   sentence and toggling hands-free are different tasks, and forcing users to
   pick one in settings is the wrong question.
3. **The gestures other tools converged on were unrepresentable.** Wispr Flow
   uses hold-a-bare-modifier; macOS Dictation's own default on external
   keyboards is double-tap Control. Neither is a chord.

There was also no cancel gesture — `StopRecordingAsync` always transcribed and
injected — and `Escape` on the widget hid it to the tray without stopping the
recording.

Windows reserves `Win+H` for Voice Typing at the shell level, so it is not
available to a hook-based application regardless of what we would prefer.

## Decision

### 1. Gestures replace the single chord

New Core types model a hotkey as a *gesture* plus an activation mode:

- `HotkeyGesture` — `Chord` (wrapping the existing `HotkeyBinding`),
  `HoldModifier`, or `DoubleTapModifier`, the latter two carrying a
  `ModifierKey` and a `ModifierSide` (`Left`/`Right`/`Either`).
- `DictationHotkey` — one gesture plus its `ActivationMode`. Users configure a
  list of these; `SettingsKeys.HotkeyBindings` stores it.

Mode is per-binding, and constrained by gesture kind: a hold must be
push-to-talk (releasing the key has to mean "stop"), a double-tap must toggle
(it has no release to hang "stop" on), and only chords support both.

### 2. Defaults

| Gesture | Mode | Why |
|---|---|---|
| Hold Right Ctrl | Push-to-talk | Same physical key on every platform, comfortable to hold. Left and right Ctrl are distinct key codes, so every normal Ctrl shortcut keeps working. |
| Double-tap Ctrl | Toggle | macOS Dictation's own default on external keyboards; collides with nothing on Windows or Linux. |
| Ctrl+Alt+Space | Toggle | Explicit fallback for environments without bare-modifier detection. Space is the safe member of the Ctrl+Alt family — AltGr is Ctrl+Alt on European layouts, so `Ctrl+Alt+<letter>` fires while typing accented characters. |

`Escape` cancels an in-progress dictation. It is hardwired rather than
configurable, matching macOS Dictation and Wispr Flow.

### 3. Gesture recognition lives in Core

`HotkeyGestureMatcher` turns normalized `HotkeyKeyEvent`s into a
`DictationAction` (`Start`/`Stop`/`Cancel`) plus a suppression decision. It owns
two pure state machines:

- `ModifierTapTracker` — a tap is a press/release inside 250 ms with no other
  key down in between. Without the second condition, `Ctrl+C` followed quickly
  by `Ctrl+V` reads as a double-tap. Two taps of the *same physical key* within
  350 ms fire the gesture.
- `ModifierHoldTracker` — starts on key-down; a non-modifier key pressed within
  a 300 ms grace window means the user reached for a shortcut, so the hold
  aborts. Later keypresses are ignored, because people do type while dictating.

`SharpHookHotkeyService` becomes a thin adapter: build the event, ask the
matcher, raise the semantic event. All gesture logic is unit-testable without a
keyboard.

### 4. Deferred hold-start when gestures share a key

The default set binds both hold and double-tap to Ctrl. Starting the hold on
key-down would make every deliberate double-tap emit Start, Cancel, Start,
Cancel, Start — a visible flicker.

When a double-tap binding shares a hold binding's physical key, the hold's start
is **deferred by the 250 ms tap window**. Releasing earlier emits nothing at all
and the tap tracker owns the gesture. Holds with no overlapping double-tap start
instantly.

This costs 250 ms of latency and leading audio in the default configuration.
Accepted: users do not begin speaking that quickly, and the alternative
(starting immediately and discarding) flickers the widget on every toggle. A
non-overlapping binding such as Hold Right Alt starts with no delay.

### 5. Suppression policy

- **Chords** are suppressed on both key-down and key-up, as before (ADR-020) —
  swallowing the release too prevents a lone key-up reaching the target app.
- **Bare modifiers are never suppressed**, even when they trigger a gesture.
  Swallowing a Ctrl key-down would break every Ctrl shortcut on the machine.
  The consequence is that a Right Ctrl hold still reads as Ctrl to other
  applications while dictating.
- **Escape** cancels — and is suppressed — only while dictation is actually
  running *and* no modifier is held. This does steal bare Escape from the
  foreground app for the duration of a recording, which is acceptable since
  recording is a deliberate and visible mode. Requiring no modifiers keeps
  Ctrl+Esc and Alt+Esc with the OS, and means a user who binds a chord
  containing Escape gets the action they asked for: every valid chord carries
  at least one modifier, so the two paths cannot collide.

### 6. The service reports intent, and is told the state

`IGlobalHotkeyService` changes shape (breaking):

- `HotkeyPressed`/`HotkeyReleased` → `DictationStartRequested`,
  `DictationStopRequested`, `DictationCancelRequested`, plus `BindingsChanged`.
- `CurrentBinding`/`UpdateBinding`/`Mode` → `Bindings`/`UpdateBindings`.
- New `SetDictationActive(bool)`.

The last one exists because the service previously guessed at toggle state with
a private `_isToggleRecording` flag, which desynced whenever recording started
or stopped by other means — the widget's own button, or a start that failed on
`CloudProviderNotConfiguredException`. `HotkeyCoordinator` observes
`TranscribeViewModel.RecordingState` and pushes the truth down. `Loading` counts
as active so Escape can abandon a start that is still waiting on the model.

### 7. Cancel is a real discard path

`TranscribeViewModel.CancelRecordingAsync()` detaches the pipeline's
`TranscriptionAvailable` handler *before* stopping it, so anything the pipeline
still produces has nowhere to go — no transcription, no injection. No Core
audio contract changed; the discard decision lives in the view model.

Unlike `StopRecordingAsync`, cancel deliberately **does not wait** on an
in-flight start. A stop waits because the user wants that recording (ADR-039);
someone pressing Escape wants out immediately, and a cold model load can run
for seconds. Model loading is synchronous native work on a thread-pool thread,
so a `CancellationToken` could not interrupt it anyway — passing one down would
leave the caller blocked just the same. Instead the cancel releases the UI at
once and sets `_cancelRequested`; the start path checks that flag on completion
and tears the recording down without ever entering the recording state. The
deferred loading spinner honours the same flag, or it would strand the widget
on "Loading model…" after the user had already cancelled.

`TranscribeWindow`'s Escape handler now cancels while recording and hides to
tray otherwise (ADR-040's behaviour is preserved for the idle case).

### 8. Two-tier validation

`HotkeyConflictDetector.Check(candidate, existing)` returns a
`HotkeyConflict` with `Blocking` or `Warning` severity:

- **Blocking** — OS-reserved shortcuts (the list gains `Win+H`, `Win+Ctrl+S`,
  `Win+Ctrl+Space`), and duplicates or overlaps within the user's own bindings.
  Hold and double-tap on the same modifier deliberately do *not* overlap; that
  pairing is the shipped default and the trackers separate them by timing.
- **Warning** — accepted but flagged: `Ctrl+Shift+Space` (IDE parameter hints)
  and `Ctrl+Alt+<letter>` (AltGr on European layouts).

Chord matching additionally ignores any event where right Alt is held, which is
what stops AltGr from firing `Ctrl+Alt+Space` on a European layout.

### 9. Migration

`HotkeySettingsMigrator.LoadOrMigrateAsync` runs once, guarded by the presence
of `SettingsKeys.HotkeyBindings`:

- A legacy chord that differs from the old `Ctrl+Shift+Space` default was
  deliberately chosen, so it becomes the user's **only** binding, keeping its
  activation mode. The new gesture defaults are *not* added on top — that would
  hand users global hotkeys they never asked for.
- An absent or still-default legacy chord yields the full new default set.

The legacy keys are read once and never written again, following the
`TranslateToEnglish` precedent.

An **empty** stored list is a decision rather than an absent setting — the
settings page lets users remove every binding and drive dictation from the
widget — so it is honoured on load. Only a genuinely missing key, or a list
whose entries all fail to decode (a damaged file), falls back to the defaults.

Bindings persist as a readable string list —
`["hold|Ctrl|Right|PushToTalk", "doubletap|Ctrl|Either|Toggle", …]` — via
`HotkeyBindingCodec`. Entries that fail to parse are dropped rather than failing
the whole load, so a settings file written by a newer version degrades instead
of breaking.

## Consequences

**Easier:**

- Push-to-talk and hands-free work simultaneously, on gestures users already
  know from macOS Dictation and Wispr Flow.
- The default no longer fights Visual Studio or VS Code.
- Gesture logic is pure and timestamp-driven, so timing behaviour is tested
  without a keyboard or a hook (43 tests across the trackers and matcher).
- Cancelling a mis-triggered recording no longer injects text.
- The record button's tooltip names the current gesture (`HotkeyHint`),
  removing the most common "what was my hotkey again" question.

**More difficult:**

- `IGlobalHotkeyService` is a breaking change; every implementation and mock had
  to be rewritten.
- The service now owns a `Timer` for deferred holds, and the matcher is reached
  from both the hook thread and the timer thread, so it is lock-guarded.
- Push-to-talk in the default configuration starts 250 ms after the key goes
  down, and that leading audio is lost.
- A held Right Ctrl is still Ctrl for other applications, since bare modifiers
  are never suppressed.
- The abort grace window (300 ms) only covers shortcuts typed at normal speed.
  A slow Right Ctrl+C leaves a short recording that stops normally; it captures
  silence, so nothing is injected, but the widget does appear.

**Verified:** besides the unit suites, an integration harness drove the real
`SharpHookHotkeyService` against a real global hook using synthesized key
events, confirming all eight scenarios end to end — hold, double-tap on and off,
a fast Right Ctrl+C producing no recording at all, typing during dictation not
aborting, ordinary Ctrl+C/Ctrl+V staying invisible, chord plus Escape, and
Escape alone passing through.

## Platform notes (not implemented)

- **Wayland** cannot grab global keys from an arbitrary client. The path is
  `org.freedesktop.portal.GlobalShortcuts`, where the compositor owns the
  binding UI — and that portal has no concept of a bare-modifier gesture, so the
  chord fallback would be the only option there. The settings page should
  degrade to "your desktop manages this shortcut" rather than showing a capture
  field that silently does nothing. See also ADR-020's Linux suppression table.
- **macOS** requires Accessibility and Input Monitoring (TCC) permission before
  libuiohook sees anything; first run should explain this before the OS prompt.
  Double-tap Ctrl collides with Apple Dictation's own default, so onboarding
  should offer double-tap Right Command (a stock preset in Apple's shortcut
  menu) as the alternative.
- **Fn/Globe** is not usable through libuiohook.
