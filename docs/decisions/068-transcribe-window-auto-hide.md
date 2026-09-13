---
status: accepted
date: 2026-09-13
---

# 068. The Transcribe Widget Hides Itself After Dictation

## Context

[ADR-040](040-frameless-compact-transcribe-window.md) modelled the Transcribe widget on
the Windows Voice Typing (Win+H) toolbar and, along with its shape, inherited its
dismissal rule: nothing hides the window except the user clicking ✕ or pressing `Esc`.
That rule made sense for the thing it was copied from and nothing else. Voice Typing is a
*session* UI — the user opens it, dictates for as long as they like, and closes it when
they are done, the same way they'd close a text editor. Parlotype's widget is not that: it
is summoned by holding a key for the length of one utterance, and it has no session to be
open for once the utterance is typed. The result, reported directly: hold the hotkey, the
widget appears; release it, and it sits always-on-top over whatever the user was dictating
into until they reach for the mouse and click ✕. Nobody asked it to appear; everybody has
to ask it to leave.

[research.md](../../plans/2026-09-13-transcribe-window-auto-hide/research.md) surveyed
what comparable dictation tools do once a session ends, and the persistent-toolbar model
turns out to be the outlier, not the norm:

- **Windows Voice Typing** — the widget this window was modelled on — genuinely never
  dismisses itself; the product's own docs describe *stopping listening* (a "Stop
  listening" state, the mic button), never an automatic close. This is the one product in
  the comparison that behaves the way Parlotype does today, and it is also the only one of
  the four that is a deliberate session UI rather than a hotkey-triggered HUD.
- **macOS Dictation** dismisses its floating mic indicator the moment dictation ends —
  "Dictation automatically ends when you stop speaking" — with a 30 s silence failsafe on
  top.
- **superwhisper**, the closest analog (local Whisper, hotkey-driven, floating recording
  window), closes its recording window once it detects the transcript was pasted; if the
  paste never happens the window stays and the text waits on the clipboard instead of
  vanishing with nowhere for the text to go.
- **Wispr Flow**'s desktop bar is persistent like Voice Typing's, but its mobile
  equivalent, the Flow Bubble, auto-minimizes after about five seconds of inactivity, and
  "any touch restores full size."

Two things follow from that table. First, auto-dismissal is the majority behaviour among
tools that, like Parlotype, summon their UI with a gesture rather than open it as a
session — Parlotype borrowed Voice Typing's *shape* deliberately (ADR-040) and inherited
its *lifetime* by accident, and the shape is worth keeping while the lifetime is not.
Second, superwhisper's rule — dismiss on evidence the text landed, not on a clock — is one
Parlotype can implement for free: `ITextInjectionService.InjectTextAsync` returning is
exactly the evidence superwhisper has to infer by watching for a paste. That evidence,
and how it does *not* line up with `IsRecording`, is the crux of this decision (below).

This ADR amends ADR-040's "Hide, don't close" section: hiding is still never closing —
recording is never disturbed by a hide, and the tray icon still reopens the window — but
hiding is no longer something only the user can trigger.

## Decision

### Ownership: whoever summons the window decides when it leaves

One rule governs everything else in this change. A Transcribe window has an owner —
`Dictation` or `User` — decided at the moment it is shown, and only the owner's rules
apply for the rest of that visible lifetime:

- Shown by a **dictation gesture** (`HotkeyCoordinator`, on hotkey press) → owned by
  `Dictation`: *transient*. It stays on screen for the entire session — recording,
  draining, transcribing, pasting — then fades out roughly 1.5 s after the session
  settles.
- Shown by the **user** — tray click, the tray "Open" menu item, a second launch
  reactivating this instance (ADR-055), or an onboarding tour step (ADR-056) — → owned by
  `User`: *sticky*, exactly as before this change. It never auto-hides.
- Shown while **already visible** → ownership does not change. A dictation gesture cannot
  demote a window the user opened themselves back to transient (FR-4); a window mid-fade
  is restored to full opacity before this check runs, so a dictation that lands during the
  grace period never has its ownership question answered by a half-hidden window.

Hiding, by any route — the fade completing, `Esc`, or ✕ — resets ownership to `None`, so
the next `Show` decides afresh. This is why no window can ever get "stuck" sticky or
"stuck" transient: ownership is a property of one visible lifetime, not of the window
object.

### The settle signal is `IsDictationBusy`, not `IsRecording`

This is the single most important mechanism in this change, because the obvious signal is
the wrong one. `AudioPipelineService.StopAsync` awaits the full drain — the segmenter
flush and the transcription of every queued utterance, up to a 30 s timeout — before it
returns, and `TranscribeViewModel.StopRecordingAsync` only clears `IsRecording` in its
`finally`, *after* that drain. So far `IsRecording` looks like a faithful "is dictation
still doing something" signal for the whole stop path.

It is not, because `TranscribeViewModel.OnTranscriptionAvailable` — the handler that fires
once a transcript is ready — is `async void`, and it awaits
`ITextInjectionService.InjectTextAsync`, whose clipboard-path implementation saves the
current clipboard, sets the transcript, sends Ctrl+V, and restores the original clipboard
contents. None of that is awaited by anything that clears `IsRecording`. `IsRecording`
goes false the moment the pipeline finishes draining; the paste that pipeline drain
*produced* is still in flight for some number of milliseconds afterward. A controller that
hid the window on `IsRecording == false` would pull the widget away mid-paste — the exact
"litter left after a session that looks finished" bug this feature exists to fix, just
moved earlier by one step.

The fix is a property nothing in the codebase had a reason to need before now:
`TranscribeViewModel.IsDictationBusy`, true from the instant a start is requested until the
last character has actually been typed — the disjunction of a start in flight
(`_startTask`), `IsRecording`, `RecordingState.Loading`, and a new `_injectionsInFlight`
counter incremented around every `InjectTextAsync` call and decremented in its `finally`.
The counter is touched from `OnTranscriptionAvailable`, which runs on the pipeline's
background task rather than the UI thread, so it is maintained with
`Interlocked.Increment`/`Decrement` and its property-changed notification is dispatched
via `Dispatcher.UIThread.Post`. `CancelAsync` (Escape, an ADR-057 command-shortcut abort)
drains with its own 5 s timeout and raises no transcription at all, so a cancelled session
clears `IsDictationBusy` as soon as the drain finishes — there is no paste to wait for
(FR-9).

### Two-tier interaction: hover pauses, commitment sticks

A dictation-owned window the user touches stops being transient, but "touches" needs two
different answers depending on how casual the touch is (research.md Q1):

- **Hover is non-committal.** The pointer merely being over a 172×112 card — on its way
  somewhere else, or because the user is glancing at it — pauses the countdown; it restarts
  from full the moment the pointer leaves (FR-6). Treating hover as a commitment would
  strand the widget on screen for a mouse motion that meant nothing.
- **Click, drag, or opening the language flyout is committal.** Any of these promotes the
  window to `User` ownership, permanently, for the rest of its visible life (FR-5). This is
  `TranscribeWindow.UserEngaged`, raised from a tunnelled `PointerPressed` handler added in
  the constructor — tunnelling so it fires before any child (the grip, a button) marks the
  event handled, meaning existing drag and button behaviour is untouched — plus
  `IsLanguageFlyoutOpen` flipping true. Keyboard *focus* is deliberately not part of this
  set; see the rejected `Window.Activated` trigger below.

**Implementation note: `GetObservable(...).Subscribe(...)` does not compile here.** The
hover pause needs to watch `TranscribeWindow.IsPointerOver`, and ownership needs to reset
when `IsVisible` goes false. The obvious-looking
`window.GetObservable(InputElement.IsPointerOverProperty).Subscribe(isOver => Evaluate())`
does not compile: `IObservable<T>` declares its own `Subscribe(IObserver<T>)`, so C# member
lookup never reaches the `Action<T>`-taking extension overload (CS1660), and that
extension's declaring type, `Avalonia.Reactive.Observable`, is `internal` to
`Avalonia.Base` anyway, so it cannot be called fully qualified either (CS0122). The shipped
code instead gives `TranscribeAutoHideController` a single `AvaloniaObject.PropertyChanged`
subscription on the window, filtered on `InputElement.IsPointerOverProperty` (re-evaluate)
and `Visual.IsVisibleProperty` going false (reset ownership) — one subscription covering
both signals. See [[../../memory/knowledge/avalonia-getobservable-subscribe-trap]] for the
full mechanism.

### Decisions on the four open questions

**No setting.** The requirements asked whether auto-hide should be configurable; the
answer is no, for a reason beyond "fewer knobs": the escape valve already exists and needs
no explanation. A user who wants the widget parked on screen opens it from the tray, or
clicks it once — both make it `User`-owned, and `User`-owned never auto-hides. That is
discoverable by doing, not by reading a checkbox, and it costs zero resx work (NFR-1), so
`check-localization.ps1` has nothing to say about this change. A duration control would
have been the worse half of a setting in any case — nobody knows whether they want 1.2 s or
2.0 s, and hover-to-pause already removes the reason to care. If this turns out to be
wrong, the upgrade is small and specifically not built yet: one boolean under
`SettingsCategory.Appearance` (alongside Theme and Interface language), key
`TranscribeWindowAutoHide`, default on, read by the controller.

**Fade out, never fade in.** The asymmetry is deliberate, not an oversight. A 172×112
always-on-top card blinking out of existence in peripheral vision reads as a glitch; a
~160 ms fade reads as a decision, and disabling input for that duration (`IsHitTestVisible
= false`) means the user can never click a card that is already on its way out. Fading the
window *in*, by contrast, would be a straightforward regression: the widget's appearance is
the user's confirmation that the hotkey registered, and delaying that confirmation by even
150 ms makes the app feel slower at exactly the moment latency is most noticeable. Appear
instantly; leave gently. `Esc` and ✕ keep their existing instantaneous hide — animation is
reserved for a hide the system initiates on its own; a user's own dismissal deserves an
immediate response, not a 160 ms tax.

**Transparency stays out of scope.** No product in the research.md comparison ships a
see-through recording window — the visibility knob every one of them offers is *show it /
don't / show a smaller one*, never alpha. The premise is also worth questioning on its own
terms: transparency is a way to make an obstacle less annoying to live with, and auto-hide
removes the obstacle outright. A window that is gone beats a window you can squint through,
and shipping both a translucent card *and* a disappearing one in the same change would be
solving overlapping problems twice. The one place alpha genuinely belongs in this feature
is the exit fade itself.

### Rejected alternatives

**`Window.Activated` as an engagement trigger.** Keyboard focus looks like an obvious third
promotion signal alongside click and flyout, but it cannot be trusted here: the dictation
path shows the window with `ShowActivated = false` specifically so summoning it does not
steal focus from whatever the user is typing into. If Windows activates the window anyway
despite that flag — a real risk worth checking by hand rather than assuming away — every
auto-summoned window would immediately read as "engaged" and the feature would silently do
nothing, with no error and no test failure to catch it. Pointer-press plus the flyout cover
every way a user actually touches a card this size, and `Esc`, the one keyboard interaction
the widget has, already hides it outright. Confirming Windows does *not* activate the
window on `Show` — and that pointer-press is therefore the only promoter that fires — is
the first item in this change's manual pass.

**A `TranscribeShowReason` parameter on `ShowTranscribe`.** Folding "why is this window
being shown" into the existing method as an enum parameter was considered and rejected in
favour of a second method, `IWindowManager.ShowTranscribeForDictation()`. `ShowTranscribe`
has four existing call sites (tray click, tray "Open", the ADR-055 reactivation path, and
the onboarding tour); a new required parameter — or a defaulted one that is easy to pass
wrong — touches all four for a distinction only one of them, the hotkey path, actually
needs. A second method with a name that states its own purpose leaves every existing call
site untouched and puts the auto-hide behaviour on the one call site it belongs to.

**A `DispatcherTimer` countdown.** The 1.5 s settle-to-hide delay is implemented as an
`async` continuation over an injectable `DelayProvider` (`Func<TimeSpan, CancellationToken,
Task>`, defaulting to `Task.Delay`) rather than a `DispatcherTimer`. A real timer would mean
headless tests either sleeping for the real 1.5 s and 160 ms or pumping the Avalonia
dispatcher to fast-forward it — slow and, in past experience with UI timers in this
codebase, flaky. An injectable delay lets `TranscribeWindowAutoHideTests` step every
countdown by hand through a `TaskCompletionSource`, so ownership, settle, hover-pause,
promotion, re-arm, error suppression, and opacity restoration are all deterministic (NFR-3)
— the same idiom `TranscribeViewModel.LoadingSpinnerDelay` already established for the
loading-spinner delay.

## Consequences

**Easier.** A clean dictation session now cleans up after itself: the widget appears
instantly on the hotkey, stays through the whole recording-drain-transcribe-paste
sequence, and fades away roughly 1.5 s later without the user reaching for the mouse. None
of that costs a setting, a resx key, or a change to `Parlotype.Core` or
`Parlotype.Platform` (NFR-1, NFR-2) — the whole feature is Desktop-only, built from a
truthful busy signal the view model didn't previously need to expose. A window the user
deliberately opened is provably unaffected: ownership is decided once, at `Show`, and a
dictation session can only take a window away from `None`, never from `User`.

**Harder / accepted.** The known wrinkle carried over from the requirements: FR-8 keeps
the window on screen after a failed session (runtime unavailable, a cloud engine not
configured, a cloud transcription failure) so the widget itself is the "something needs
attention" signal — but ADR-040 already made the status text tooltip-only, so the *reason*
for that failure is still something the user only sees by hovering, not something the card
states outright. This ADR does not change that; it inherits the tradeoff and leaves making
the error visible on the card itself as a follow-up, not something fixed here (task.md,
"Out of scope"). Ownership and the countdown now depend on the still-unverified assumption
that `ShowActivated = false` actually prevents activation on the dictation path across the
Windows versions this ships to — the first thing the manual pass checks, with the fallback
already decided (keep `Activated` out of the engagement set; nothing else changes) if it
does not hold. `TranscribeAutoHideController` is a new per-window object whose
subscriptions — view-model property changes, and a single `AvaloniaObject.PropertyChanged`
subscription on the window filtered to `IsPointerOverProperty`/`IsVisibleProperty` (see the
implementation note above) — are wired in its constructor and released in `Dispose`
alongside the window's own lifetime rather than through DI, so getting that teardown wrong
would show up as a countdown that keeps firing against a disposed window rather than as a
compiler or DI error. That risk is real, not hypothetical: testing this controller
surfaced exactly that shape of bug in the *test* harness itself — disposing it only
cancels an in-flight countdown, which merely *schedules* the rest of its cleanup onto the
shared headless test dispatcher rather than running it before `Dispose` returns — see
[[../../memory/knowledge/avaloniafact-drain-dispatcher-after-dispose]].
