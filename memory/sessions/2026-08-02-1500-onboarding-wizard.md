---
title: "Session: 2026-08-02 — First-run onboarding wizard"
type: session
status: active
tags: [onboarding, wizard, adorner, localization, settings, help, adr-055]
created: 2026-08-02
summary: "Built the 8-step first-run onboarding tour (ADR-055): floating Topmost wizard that opens real windows and highlights live controls via OnboardingTarget.Id + AdornerLayer; OnboardingCompleted flag; Settings → Help; first Strings.resx localization layer."
---

# Session: 2026-08-02 — First-run onboarding wizard

## Active Focus

The app starts tray-only, so a fresh install shows literally nothing. Built a
step-by-step onboarding tour whose differentiator is that each step **opens the
real app window and highlights the live controls** it describes. Plan folder:
`plans/2026-08-02-onboarding-wizard/` (completed). ADR:
`docs/decisions/055-first-run-onboarding-wizard.md`.

New code: `src/Parlotype.Desktop/Onboarding/*` (target markers, highlight
adorner + service, step model/factory), `ViewModels/Onboarding/*`,
`Views/OnboardingWindow.axaml(.cs)`, `Services/IOnboardingService.cs` +
`OnboardingService.cs`, `ViewModels/Settings/HelpSettingsViewModel.cs` +
`Views/Settings/HelpSettingsView.axaml`, `Resources/Strings.resx` + `Strings.cs`.
Modified: `SettingsKeys` (+`OnboardingCompleted`), `SettingsSection`
(+`Engine`/`EngineModel`/`Help`), `SettingsWindowViewModel` (17th section +
`NavigateTo` arms + `ModelSectionForActiveEngine`), `SpeechEngineDisplayItem`
(+`OnboardingId`), marker attributes in TranscribeWindow / engine + model
views, `App.axaml.cs` (DI + `MaybeShowOnFirstRunAsync`).

## Decisions Made

- **User decision:** auto-show is a plain `OnboardingCompleted` settings flag,
  not a Velopack `OnFirstRun` gate — fresh installs, updaters and first dev
  runs each see the tour once. Flag stamped **before** showing.
- Highlight = attached-property markers + `AdornerLayer` pulsing adorner
  (layout-tracking free); pending ids retried on `LayoutUpdated`; missing or
  invisible targets silently skipped.
- Cloud step targets the two cloud engine *cards* on the Engine page — the
  Cloud providers section is engine-filtered away under any local engine and
  `NavigateTo` would silently no-op.
- `SettingsSection.EngineModel` resolves per active engine; cloud engines fall
  back to the Engine page.
- Copy externalized to resx behind a hand-written public accessor; markup only
  binds VM properties (dynamic hotkey text forces that shape anyway).

## Facts Learned

- **`RaiseEvent(Button.ClickEvent)` never executes a bound `Command`** — it
  bypasses `Button.OnClick`. Cost three failing window tests; fixed by real
  `MouseDown`/`MouseUp` simulation. Distilled to
  `memory/knowledge/avalonia-click-event-vs-command.md`.
- `AdornerLayer.SetAdorner` works under Avalonia.Headless (FluentTheme window
  template supplies the layer) — the plan's top risk evaporated at the spike.
- MVVMTK0034: `[ObservableProperty]` backing fields cannot be assigned even
  inside the owning class — `Start()` re-raises notifications by hand for the
  index-already-0 re-launch case instead.

## Open Blockers

None. One verification gap: the live desktop walk-through of all 8 steps
(pulse visuals, wizard placement next to each window) could not be done —
the user's screen was occupied by a fullscreen game during the session, so
verification was Win32-level only (tour window auto-opens on first launch,
absent on second; flag round-trip). The flag was **removed again** from the
user's real `settings.json` afterwards, so their next launch of the new build
will show the tour for real.

## Documentation Status

- ADR: done — `docs/decisions/055-first-run-onboarding-wizard.md`.
- Vault: done — `memory/services/desktop.md` (onboarding entries + fixed stale
  `SilentModelDownloadService` row + Application category),
  `memory/decisions/_index.md` (055 row),
  `memory/architecture/subsystems.md` (Onboarding Tour + Localization
  sections).
- Knowledge: done — `avalonia-click-event-vs-command.md` + index row.
- Plans: folder completed, `INDEX.md` row moved to Completed.

## Next Action

Human visual pass over the tour at a free desktop: run the app with
`OnboardingCompleted` absent, walk all 8 steps, and check (a) highlight pulses
land on the right elements per engine (Parakeet default hides the language
strip — the widget step should silently skip that target), (b) wizard
placement flips left correctly near the right screen edge, (c) the two
Topmost windows never deadlock in z-order. Then commit — nothing is committed
yet; the branch is `claude/parlotype-onboarding-wizard-14bf3f`.
