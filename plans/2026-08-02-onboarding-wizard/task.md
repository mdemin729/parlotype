---
title: First-run onboarding wizard with live UI highlighting
status: completed
created: 2026-08-02
started: 2026-08-02
completed: 2026-08-02
---

# First-run onboarding wizard

## Problem

Parlotype starts tray-only: `App.OnFrameworkInitializationCompleted` never sets
`MainWindow`, so after installation a new user sees literally nothing — no
window, no hint that hotkeys exist, no way to discover the recording widget or
the Settings window. There is no onboarding of any kind, no first-run flag, and
no in-app help.

## Approach

A step-by-step onboarding wizard in a separate compact always-on-top window
(Back / Next / Skip + progress dots) that, on each step, **opens the real app
window it describes and highlights the live UI elements** via a new
attached-property marker (`OnboardingTarget.Id`) + `AdornerLayer` pulsing
outline. Eight steps: welcome (local-by-default), recording (real configured
hotkeys via `IGlobalHotkeyService` + `DisplayString`/`HotkeyHint`), widget
anatomy, engine choice, model choice (deep link resolves to the active engine's
model page), cloud engines (opt-in/BYOK), tray behaviour, recap.

Auto-shown once, gated by a new `SettingsKeys.OnboardingCompleted` string-bool
flag written at show time (user decision: anyone without the flag sees it once —
fresh installs, updaters, first dev run). Re-launchable from a new
**Settings → Help** section that also lists the current hotkey bindings.

All copy externalized to `Resources/Strings.resx` (the repo's first
localization layer) behind a hand-written static accessor; markup only binds VM
properties.

Full details: [implementation-plan.md](implementation-plan.md).

## Workplan

- [x] Core settings key `OnboardingCompleted`
- [x] `Strings.resx` + `Strings` accessor (all wizard/Help copy)
- [x] `SettingsSection.Engine/EngineModel/Help` + `NavigateTo` mapping
- [x] `OnboardingTarget` attached property + markers in TranscribeWindow /
      engine cards / model lists
- [x] `OnboardingHighlight` adorner + `OnboardingHighlightService` (headless
      spike first — AdornerLayer confirmed working under Avalonia.Headless)
- [x] Step model + `OnboardingStepFactory` + `OnboardingWizardViewModel`
- [x] `OnboardingWindow` (placement next to target, poll for window, highlight)
- [x] `HelpSettingsViewModel/View` + 17th settings section
- [x] `IOnboardingService`/`OnboardingService` + App wiring
- [x] Tests (factory, VM, service, highlight, window, Help, Strings,
      SettingsWindowViewModelTests update) — 385 Desktop tests green
- [x] Build/test green (0 warnings, 1109 tests), ADR-055, vault updates
- [x] Live verification: first launch auto-opened "Parlotype tour"
      (Win32-verified — screen was occupied by a fullscreen game, so no
      screenshot), flag stamped `"True"`, second launch tray-only; flag then
      removed again so the user's own first run still shows the tour.
      Visual walk-through of all 8 steps (highlight pulses, placement) is left
      for a human at a free desktop
