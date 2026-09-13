# Research — auto-hiding the Transcribe widget

Background for [task.md](task.md). Three parts: what the widget does today, what
comparable products do, and the reasoning behind each of the four open questions.

## 1. What happens today

**Who shows the window** (nobody hides it automatically):

| Call site | Purpose | Activates? |
|-----------|---------|-----------|
| [HotkeyCoordinator.cs:78](../../src/Parlotype.Desktop/Services/HotkeyCoordinator.cs:78) | a dictation gesture started | no |
| [AppViewModel.cs:42](../../src/Parlotype.Desktop/ViewModels/AppViewModel.cs:42), [:55](../../src/Parlotype.Desktop/ViewModels/AppViewModel.cs:55) | tray menu "Open" / tray click | yes |
| [App.axaml.cs:120](../../src/Parlotype.Desktop/App.axaml.cs:120) | a second launch reactivates this one (ADR-055) | yes |
| [OnboardingWizardViewModel.cs:139](../../src/Parlotype.Desktop/ViewModels/Onboarding/OnboardingWizardViewModel.cs:139) | tour steps that highlight the widget (ADR-056) | no |

**Who hides it:** the ✕ button and `Esc`, both through `HideToTray()` in
[TranscribeWindow.axaml.cs](../../src/Parlotype.Desktop/Views/TranscribeWindow.axaml.cs).
`WindowManager.HideTranscribe()` exists on `IWindowManager` and has **no production
caller at all** — it is dead API today.

So the reported behaviour is exact: the hotkey summons the widget, and only a manual
✕/`Esc` ever takes it away. Four of the five entry points are deliberate user acts; the
fifth — the dictation gesture — is not, and it is the one that leaves litter.

**Pipeline timing facts that constrain "when is dictation over":**

- `AudioPipelineService.StopAsync` → `ShutdownAsync(discard: false)` **awaits the full
  drain** (segmenter flush + transcription of every queued utterance, up to a 30 s
  timeout). So when `TranscribeViewModel.StopRecordingAsync` returns, every
  `TranscriptionAvailable` event has already been raised.
- `IsRecording` is only cleared in that method's `finally`, i.e. **after** the drain —
  so it is already a faithful "still working" signal for the whole stop path.
- But `TranscribeViewModel.OnTranscriptionAvailable` is `async void` and awaits
  `ITextInjectionService.InjectTextAsync`, which on the clipboard path saves the
  clipboard, sets it, sends Ctrl+V and restores it. **That work outlives `IsRecording`.**
  Hiding the widget the moment `IsRecording` goes false would pull it away mid-paste.
- `CancelAsync` (Escape, ADR-057 command aborts) drains with a 5 s timeout and raises no
  transcription at all.

That last point is the single non-obvious requirement in this whole feature: the
"dictation is finished" moment is **not** `IsRecording == false`, it is
`IsRecording == false && nothing is being typed`.

## 2. What comparable products do

| Product | While dictating | After dictation | User control | See-through? |
|---|---|---|---|---|
| **Windows Voice Typing** (Win+H) — the widget ADR-040 was modelled on | toolbar visible | **stays until dismissed**; the docs describe stopping *listening* ("Stop listening", the mic button) and never an automatic dismissal | close button | no |
| **macOS Dictation** | floating mic indicator | **disappears when dictation ends** — "Dictation automatically ends when you stop speaking", plus a 30 s silence failsafe | the auto-end checkbox | no (system material) |
| **superwhisper** (closest analog: local Whisper, hotkey-driven, floating recording window) | recording window | **closes once it detects the transcript was pasted**; if the paste never happens the window stays and the text waits in the clipboard | `Auto-Close Window` = close "regardless of paste status"; `Recording Window Enabled` = never show it; `Mini Recording Window`; `Always show Mini Recording Window` | no opacity setting documented |
| **Wispr Flow** | Flow Bar / Flow Bubble | the desktop bar is **persistent**; the Android bubble "auto-minimizes after about 5 seconds of inactivity… Any touch restores full size" | right-click "Hide for 1 hour"; Settings → System → Show Flow Bar | no |

Four things fall out of that table:

1. **Auto-dismissal is the majority behaviour among dictation-first tools.** The
   persistent-toolbar model is Windows Voice Typing's, and Voice Typing is a dictation
   *session* UI you enter and leave deliberately — not a HUD summoned by a held key.
   Parlotype borrowed Voice Typing's *shape* (ADR-040) and inherited its *lifetime* by
   accident. The shape is worth keeping; the lifetime is not.
2. **The nearest analog dismisses on an outcome, not on a clock.** superwhisper waits for
   evidence the text landed. Parlotype gets that evidence for free — `InjectTextAsync`
   returning is the thing superwhisper has to *detect* — so we can implement the better
   version of their default at no cost.
3. **Interaction defers dismissal** (Wispr: "Any touch restores full size"). Nobody yanks
   a widget out from under the pointer.
4. **Nobody ships a see-through recording window.** The visibility knob these products
   offer is *show it / don't / show a smaller one* — never alpha.

## 3. The four questions

### Q1 — Should interacting with the window make it stay?

**Yes, and it should be two-tier.** Merging hover and click into one rule gets one of them
wrong: if hovering made the widget permanent, a mouse that merely crosses a 172×112 card
on its way elsewhere would strand it; if clicking only *paused* the countdown, a widget
the user just dragged into place would slide away the moment they let go.

- **Hover** is non-committal → pause the countdown, restart it in full on pointer-leave.
  This is how every auto-dismissing HUD behaves.
- **Click, drag, flyout, keyboard focus** are committal → the window becomes
  *user-owned* and auto-hide is off for good, until it is next hidden.

The instinct in the requirements ("I already interacted with it, so it should stay") is
exactly right; it just needs the hover carve-out so an accidental mouse crossing is
harmless.

There is a second, cheaper half of the same idea that matters more: **the window the user
opened from the tray must never auto-hide at all.** Ownership is decided by whoever
summoned it. That one rule is what makes a settings toggle unnecessary (see Q2).

### Q2 — Should this be configurable?

**No setting in v1.** Agreeing with the requirements, and with a concrete reason beyond
"fewer knobs":

- The escape valve already exists and needs no explanation. Want the widget parked on
  screen? Open it from the tray, or click it once — both make it user-owned, and
  user-owned never auto-hides. That is discoverable by doing, not by reading a checkbox.
- Every user-facing string costs three resx files (CLAUDE.md's localization rule), so a
  toggle is never one line here.
- A *duration* control is the worse half of the idea in any case: nobody knows whether
  they want 1.2 s or 2.0 s, and hover-to-pause removes the reason to care.

If that turns out to be wrong, the upgrade is small and pre-planned: one boolean under
`SettingsCategory.Appearance` (where Theme and Interface language already live), key
`TranscribeWindowAutoHide`, default on, read by the controller. Worth **not** doing until
someone actually asks.

### Q3 — Animation?

**Fade out: yes. Fade in: no.** The asymmetry is the point.

- A 172×112 always-on-top card that blinks out of existence in peripheral vision reads as
  a glitch; ~160 ms of fade reads as a decision. It is also the only honest way to signal
  "this is about to go" — during the fade the window stops accepting clicks, so the user
  never clicks a card that is already leaving.
- Fading *in* is actively worse than not animating. The widget appearing is the
  confirmation that the hotkey registered; delaying that by even 150 ms makes the app feel
  slower exactly where latency is most noticed. Appear instantly, leave gently.
- Cost is low and the idiom is already in the codebase — `TranscribeWindow.axaml` already
  animates a `Border`'s `Opacity` with a `DoubleTransition` for the record button.

So: not over-engineering on the way out, and a mistake on the way in.

### Q4 — Transparency?

**Not as part of this change.** Per the table above, no comparable product makes its
recording widget see-through; what they use is an opaque surface (sometimes a blurred
system material, which is a different thing — it stays fully legible).

And the premise is worth questioning: transparency is a way to make an obstacle less
annoying. Auto-hide removes the obstacle. A window that is gone beats a window you can
squint through, and stacking both would mean a translucent card *and* a disappearing one.

The one place alpha genuinely belongs is the exit animation itself — which is Q3. If
"less obtrusive while visible" still feels unsolved after this ships, the follow-up worth
considering is Avalonia's `AcrylicBlur` transparency hint or a reduced idle opacity, as
its own plan with its own before/after screenshots.

## 4. Numbers

| Constant | Value | Why |
|---|---|---|
| Settle → hide delay | **1.5 s** | Long enough to register the widget finishing; short enough that it is gone before attention returns from the text that just appeared. Hover pauses it, so precision does not matter. |
| Fade-out duration | **160 ms** | Below ~100 ms reads as a cut, above ~250 ms as sluggish for a dismissal. |
| Re-arm after pointer-leave | full 1.5 s | Restarting is simpler to reason about than resuming a partial countdown, and a user who just left the window is likelier to come back. |

## Sources

- [Use voice typing to talk instead of type on your PC — Microsoft Support](https://support.microsoft.com/en-us/windows/use-voice-typing-to-talk-instead-of-type-on-your-pc-fec94565-c4bd-329d-e59a-af033fa5689f)
- [Recording Window — Superwhisper docs](https://superwhisper.com/docs/get-started/interface-rec-window)
- [Advanced Settings — Superwhisper docs](https://superwhisper.com/docs/get-started/settings-advanced)
- [Troubleshooting the Flow Bar (Desktop) and Flow Bubble (Android) — Wispr Flow Help Center](https://docs.wisprflow.ai/articles/5002934560-why-is-the-wispr-bar-is-not-appearing-or-disappearing)
- [Navigating the Wispr Flow App — Wispr Flow Help Center](https://docs.wisprflow.ai/articles/5096240724-navigating-the-wispr-flow-app-desktop-ios-and-android)
- [Mac dictation keeps stopping — the auto-end-on-silence setting](https://whisper.remskill.com/blog/mac-dictation-keeps-stopping)
