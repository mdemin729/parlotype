---
title: The Transcribe widget hides itself after dictation
status: completed
created: 2026-09-13
started: 2026-09-13
completed: 2026-09-13
---

# The Transcribe widget hides itself after dictation

## Problem

Hold the dictation hotkey and the Transcribe widget appears; release it, and the widget
stays on screen — always-on-top, over whatever the user was dictating into — until they
move the mouse to it and click ✕. Nobody asked for it to appear, but everybody has to ask
for it to leave. The window is a heads-up display for a gesture that lasted three seconds,
and it outlives that gesture indefinitely.

That is not a bug in any one place; it is an inherited lifetime.
[ADR-040](../../docs/decisions/040-frameless-compact-transcribe-window.md) modelled the
widget on the Windows Voice Typing (Win+H) toolbar and took its dismissal rule along with
its shape — but Voice Typing is a *session* the user enters and leaves deliberately, while
Parlotype's widget is summoned by a held key. Dictation-first tools that work the way
Parlotype does (superwhisper, macOS Dictation) dismiss their recording window on their
own. See [research.md](research.md) for the comparison and the sources.

## Approach

**Whoever summoned the window decides when it leaves.** That one rule settles almost
everything, including the open questions in the requirements:

- Summoned by a **dictation gesture** → the window is *transient*. It stays for the whole
  session (recording, draining, transcribing, typing), then fades out ~1.5 s after the
  session settles.
- Summoned by the **user** — tray click, tray "Open", a second launch, the onboarding tour
  — → the window is *sticky*, exactly as today. It never auto-hides.
- A transient window the user **touches** — clicks, drags, opens the language flyout,
  focuses — is promoted to sticky, permanently. Merely *hovering* is not a commitment: it
  pauses the countdown, which restarts in full when the pointer leaves.

Consequences worth stating up front, because they are what makes the design cheap:

- **No new setting and no new user-facing string.** "I want it to stay" already has an
  answer — open it from the tray, or click it once. Zero resx work, so
  `check-localization.ps1` has nothing to say about this change.
- **`Esc`/✕ keep working unchanged**, and stay instantaneous. Animation is for
  system-initiated changes; a user's own click deserves an immediate response.
- **The settle signal is not `IsRecording`.** `StopAsync` drains and transcribes before it
  returns, but text injection (`async void` → `InjectTextAsync`) outlives it. The widget
  must not vanish mid-paste, so the view model needs a truthful "still busy" property that
  covers the injection too.

### Decisions on the four open questions

| # | Question | Decision |
|---|----------|----------|
| 1 | Should interaction keep the window? | **Yes, two-tier.** Click/drag/flyout/focus → sticky forever. Hover → pause the countdown only, re-arm on leave. Plus the stronger rule above: a user-opened window never auto-hides in the first place. |
| 2 | Add a setting? | **No.** Not a toggle, and definitely not a duration. The tray-open and click-to-keep paths are the escape valve. Pre-planned upgrade path recorded in research.md if we are proven wrong. |
| 3 | Animation? | **Fade out (~160 ms), never fade in.** The exit needs to read as intentional rather than as a glitch; the entrance is the confirmation that the hotkey registered and must be instant. |
| 4 | Transparency? | **No — out of scope, and mostly answered by the feature itself.** No comparable product ships a see-through recording window. Auto-hide removes the obstruction; transparency only makes an obstruction easier to live with. The only alpha in this change is the fade-out. |

## Requirements

### Functional

- **FR-1** A Transcribe window summoned by a dictation gesture hides itself automatically
  once the session has fully settled and a grace delay has elapsed.
- **FR-2** "Settled" means: no start in flight, not recording, not loading a model, and no
  text injection outstanding. The window must remain visible for the entire drain +
  transcription + paste.
- **FR-3** A window the user opened (tray click, tray "Open" menu item, a second launch,
  the onboarding tour) never auto-hides.
- **FR-4** A window that was already visible when dictation started keeps its existing
  ownership — a dictation session cannot take away a window the user opened.
- **FR-5** Clicking anywhere in the window, dragging it by the grip, or opening the
  language flyout promotes a transient window to sticky for the rest of its visible life.
  Window *activation* is deliberately not a trigger — see the implementation plan.
- **FR-6** While the pointer is over the window the countdown is suspended; it restarts
  from full on pointer-leave.
- **FR-7** Starting a new dictation (or any other return to a busy state) during the grace
  period cancels the pending hide.
- **FR-8** A session that ends in a failure state (runtime unavailable / restart required,
  cloud engine not configured, cloud transcription failure) does **not** auto-hide — the
  widget stays as the "something needs attention" signal. The next clean session hides it
  again, so the state self-heals.
- **FR-9** A cancelled session (`Esc`, or an ADR-057 command-shortcut abort) counts as a
  clean end and auto-hides.
- **FR-10** The automatic hide fades the card out over ~160 ms; the window stops accepting
  input as soon as the fade starts, and full opacity is restored before it is ever shown
  again.
- **FR-11** ✕ and `Esc` hide immediately, with no fade, exactly as today.
- **FR-12** Hiding never stops or disturbs recording (unchanged ADR-040 rule) — though
  under these rules an auto-hide can only happen when nothing is running.

### Non-functional

- **NFR-1** No new persisted setting, no new resx key, no change to any `Strings.*`.
- **NFR-2** No change to `Parlotype.Core` or `Parlotype.Platform` — this is Desktop-only.
- **NFR-3** Timings are injectable so headless tests do not sleep for real seconds
  (the existing `TranscribeViewModel.LoadingSpinnerDelay` idiom).
- **NFR-4** `dotnet build Parlotype.slnx` clean with zero warnings; `dotnet test` green.

### Out of scope

- Window transparency / acrylic material while visible (see research.md Q4).
- A "never show the widget at all" mode.
- Making the error status visible on the card itself — FR-8 keeps the window up, but
  ADR-040's tooltip-only status means the *reason* is still only in the tooltip. Worth a
  follow-up; not this change.

## Workplan

- [x] `TranscribeViewModel`: a truthful `IsDictationBusy` (start pending / recording /
      loading / injection in flight) and `IsInErrorState` derived from `_statusKind`
- [x] `TranscribeWindow`: pointer-over + engagement signals, and a cancellable
      `HideWithFadeAsync()` that restores opacity
- [x] `TranscribeAutoHideController` (Desktop/Services): the ownership + countdown state
      machine, with injectable `Delay` / `FadeDuration`
- [x] `IWindowManager.ShowTranscribeForDictation()`; `WindowManager` creates and drives the
      controller; `HotkeyCoordinator` calls the new method
- [x] Update the three `IWindowManager` stubs (`MockWindowManager`, and the two
      `DesignWindowManager` nested classes)
- [x] Headless tests: ownership, settle, hover-pause, engagement promotion, re-arm,
      error suppression, opacity restore; view-model tests for the busy signal
- [x] Manual pass on Windows: hover-pause, fade, cloud error path and onboarding all
      verified 2026-09-13
- [x] ADR-068 (amends ADR-040's "hide, don't close"); annotate ADR-040
- [x] Vault: `memory/services/desktop.md`, `memory/architecture/subsystems.md`,
      `memory/decisions/_index.md`; session note

Step-by-step detail: [implementation-plan.md](implementation-plan.md).

## Manual verification (Windows, 2026-09-13) — all items pass

Exercised by the user on a real Windows desktop. Headless tests can drive the state
machine; none of the four could be proven without a real compositor and a real paste.

1. **Pointer-over hover-pause on the real frameless transparent window — works.** The
   feature's single biggest risk: `TranscribeWindow` is `WindowDecorations="None"` with a
   transparent background over a rounded `RootChrome` border (ADR-040), and
   `InputElement.IsPointerOver` under the Windows compositor could plausibly have differed
   from `Avalonia.Headless` (flagged in research.md and in ADR-068's rejected-alternatives
   section). It does not — the `PointerEntered`/`PointerExited`-on-`RootChrome` fallback is
   **not** needed.
2. **Fade reads as intentional, no flash.** The ~160 ms `RootChrome` opacity transition is
   smooth, and the `HideWithFadeAsync`/`RestoreChrome` ordering produces no one-frame flash
   of an opaque card.
3. **FR-8's sticky card on a failed session — works.** A cloud misconfiguration leaves the
   widget up instead of fading, and the next clean dictation clears it, so the error state
   latches visibly but not permanently. Repro used (needs no API key, no network, no spend —
   `OpenAiCompatibleSpeechRecognizer` throws `CloudProviderNotConfiguredException(MissingApiKey)`
   before it ever builds a request): baseline fade on Parakeet → switch to OpenAI-compatible
   with no stored key → dictation hotkey → dismiss the dialog → card stays → switch back to
   Parakeet and dictate → card fades ~1.5 s after the text lands.
4. **Onboarding tour unaffected**, as predicted — it calls plain `ShowTranscribe`, so its
   windows are `User`-owned and never auto-hide.

Implicitly confirmed by (1) and (2): Windows does **not** activate the dictation-summoned
window despite `ShowActivated = false`. Had it done so, every auto-summoned window would
have read as instantly "engaged" and the widget would never have hidden at all — which is
exactly what the rejected `Window.Activated` engagement trigger would have caused.

The known wrinkle from "Out of scope" stands and was accepted in use: the sticky error card
carries its reason only in the tooltip, because ADR-040 made status tooltip-only. Surfacing
it on the card is a follow-up, not a defect in this change.
