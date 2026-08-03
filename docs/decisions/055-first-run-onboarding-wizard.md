---
status: accepted
date: 2026-08-02
---

# 055. First-Run Onboarding Wizard with Live UI Highlighting

## Context

Parlotype starts tray-only: `App.OnFrameworkInitializationCompleted` never sets
`MainWindow`, so after installation a new user sees *nothing* — no window, no
hint that dictation hotkeys exist, no path to the recording widget or Settings.
The three discovery mechanisms the app relies on (tray icon, global hotkeys,
tooltips on a window the user has never seen) all presuppose the user already
knows they exist.

A conventional text-or-screenshot walkthrough was rejected up front: static
pictures go stale with every UI change, and they cannot show the user *their
own* configuration (the actual hotkey bindings, the actual engine). The
requirement was a tour that opens the real windows and points at the real
controls.

## Decision

A step-by-step onboarding tour, implemented entirely in `Parlotype.Desktop`
(no new Core interface — Core only gains the `SettingsKeys.OnboardingCompleted`
constant, because that is where settings keys live).

**Tour mechanics.** `OnboardingWindow` is a frameless Topmost card (380 px,
matching the ADR-040 widget chrome) with Back/Next/Skip, a "Step N of M" label
and progress dots. The step list is declarative:
`OnboardingStepFactory.Build(bindings)` returns eight `OnboardingStep` records
(welcome, recording, widget anatomy, engine, model, cloud, tray, recap). On
each step change `OnboardingWizardViewModel` opens the step's target window
through the existing `IWindowManager`, and the window's code-behind polls the
desktop lifetime for the target (`WindowManager` posts fire-and-forget, so
there is no completion signal), repositions itself beside it (right side,
clamped to the working area, flipping left when out of room), and applies the
highlights. Steps with dynamic copy compose it from live services: the
recording step lists each valid `DictationHotkey` via its existing
`DisplayString`/`ModeLabel`, honouring a deliberately emptied binding list
(ADR-047) with a fallback line instead of resurrecting defaults.

**Highlighting.** A new attached property `OnboardingTarget.Id` marks
highlightable controls in AXAML (`RecordButton`, `GripZone`, `CloseButton`,
`LanguageStrip`, the engine-cards list, per-engine cards via
`Settings.EngineCard.{SpeechEngine}`, and the three model lists sharing
`Settings.ModelList`). `OnboardingHighlightService.Apply(window, ids)` scans
the visual tree for marked controls and attaches a pulsing-outline
`OnboardingHighlight` control with `AdornerLayer.SetAdorner` — the adorner
layer re-arranges over the adorned control on every layout pass, so the
118↔88 widget height flip, strip visibility changes and the Settings content
swap need zero tracking code. Ids not yet found (content that materializes
after navigation) stay pending and are retried on `Window.LayoutUpdated`;
missing or invisible targets are silently skipped — a step without its target
still shows its text. The pulse animation uses the house idiom
(`DispatcherTimer` + `Render`, like `WaveformView`), not Avalonia Animations.
Verified working under Avalonia.Headless with the FluentTheme window template.

**Deep links.** `SettingsSection` gains `Engine`, `EngineModel` and `Help`.
`EngineModel` resolves dynamically to the active engine's model page
(Parakeet/Whisper/Gemma 4) and falls back to the Engine page for cloud
engines, which have no local model page. The cloud step deliberately targets
the two cloud engine *cards* on the Engine page rather than the Cloud
providers section, which is invisible under any local engine (ADR-043
filtering) and would silently no-op.

**Once-only semantics.** `SettingsKeys.OnboardingCompleted` follows the house
string-bool default-off convention. `OnboardingService.MaybeShowOnFirstRunAsync`
(fire-and-forget from `App.OnFrameworkInitializationCompleted`, exceptions
swallowed) stamps the flag `"True"` *before* showing, so a crash mid-tour
still counts as offered, and a failing settings write skips the tour rather
than showing it on every launch. Deliberately **not** gated on Velopack's
`OnFirstRun`: anyone without the flag sees the tour once — fresh installs,
existing users after this update (which doubles as feature discovery), and the
first dev run. The Velopack hook never fires for dev builds and must not show
UI (ADR-053), so it was unsuitable anyway.

**Re-launch.** A new always-visible **Settings → Help** section
(`SettingsCategory.Application`) hosts an "Open the tour" button and a live
hotkey reference (rebuilt on `BindingsChanged`).

**Localization.** All tour and Help copy lives in
`Resources/Strings.resx` behind a hand-written `Strings` accessor
(`ResourceManager`, key-name fallback) — the repo's first externalized-strings
layer. Markup never hardcodes tour copy; every caption binds a VM property
sourced from `Strings`, so a future translation is a satellite
`Strings.<culture>.resx` with no markup changes. The accessor is hand-written
(not designer-generated) to stay deterministic under CLI builds with
warnings-as-errors, and public so a test can verify every property resolves to
real resx content.

## Consequences

- New users finally see *something* on first launch, and the tour teaches with
  the live UI, so it cannot drift from reality the way screenshots would.
- The tour never triggers downloads or engine switches — it only calls
  `ShowTranscribe`/`ShowSettings(section)`; model downloads still happen only
  on explicit user action.
- `OnboardingTarget.Id` markers are a stable, compile-checked (via
  `OnboardingTargetIds` consts + `{x:Static}`) contract between views and the
  tour; future tour steps only need a marker and a step record.
- The `Strings` layer creates the precedent for externalizing the rest of the
  app's copy; user-visible strings still hardcoded elsewhere (including in
  Core: `HotkeyHint`, `DictationHotkey.ModeLabel`) are unchanged for now.
- The wizard repositions only on step change — it does not track the target
  window while the user drags it mid-step. Accepted as cosmetic.
- Two Topmost windows (wizard + widget) rely on activation order for z-order;
  the wizard re-activates itself after each step's target appears.
- `SettingsWindowViewModel` grew a 17th section; the positional
  `BuildViewModel` helper in tests and the full-list nav assertions had to be
  updated — the known cost of the compile-time wiring (ADR-028).
