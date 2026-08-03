---
title: "Session: 2026-08-02 — First-run onboarding wizard"
type: session
status: active
tags: [onboarding, wizard, adorner, localization, settings, help, focus, adr-055]
created: 2026-08-02
summary: "Built the 8-step first-run onboarding tour (ADR-055): floating Topmost wizard that opens real windows and highlights live controls via OnboardingTarget.Id + AdornerLayer; OnboardingCompleted flag; Settings → Help; first Strings.resx localization layer. Fixed a keyboard-focus bug on the Settings steps. Shipped as PR #14."
---

# Session: 2026-08-02 — First-run onboarding wizard

## Active Focus

The app starts tray-only, so a fresh install shows literally nothing. Built a
step-by-step onboarding tour whose differentiator is that each step **opens the
real app window and highlights the live controls** it describes. Plan folder:
`plans/2026-08-02-onboarding-wizard/` (completed). ADR:
`docs/decisions/055-first-run-onboarding-wizard.md`.
PR: [#14](https://github.com/mdemin729/parlotype/pull/14) (open, 3 commits).

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
- **Keyboard:** Next is `IsDefault` *and* explicitly focused at the end of each
  step. Both, not either — `IsDefault` alone leaves no visible focus ring, and
  focusing alone breaks the moment focus lands on a non-button.

## Facts Learned

- **`RaiseEvent(Button.ClickEvent)` never executes a bound `Command`** — it
  bypasses `Button.OnClick`. Cost three failing window tests; fixed by real
  `MouseDown`/`MouseUp` simulation. Distilled to
  [[avalonia-click-event-vs-command]].
- **`Window.Activate()` gives the window foreground but focuses nothing inside
  it**, and a window shown from a queued `Dispatcher.Post` activates *after* a
  caller that didn't yield — the two causes of the focus bug below. Distilled
  to [[avalonia-window-activation-focus]].
- `AdornerLayer.SetAdorner` works under Avalonia.Headless (FluentTheme window
  template supplies the layer) — the plan's top risk evaporated at the spike.
- `IFocusManager` in Avalonia 12 has no `ClearFocus`; clear focus with
  `Focus(null)`.
- MVVMTK0034: `[ObservableProperty]` backing fields cannot be assigned even
  inside the owning class — `Start()` re-raises notifications by hand for the
  index-already-0 re-launch case instead.
- **`SetForegroundWindow` from a background process is silently ignored** by
  Windows, so `SendKeys` verification scripts deliver keystrokes to whatever
  the user actually has focused (they leaked into the user's IDE here). For
  driving an app under test, `PostMessage(WM_KEYDOWN/WM_KEYUP)` to the target
  HWND plus `GetGUIThreadInfo(uiThreadId).hwndFocus` for reading focus is both
  safe and independent of foreground state.

## Open Blockers

None. The one bug found after the first commit — Next not focused on the three
Settings steps, so Enter did nothing — is fixed (commit `5f29762`) and verified
both headlessly and live. The user has since completed the manual visual pass
over the tour and reports it working, which closes the last verification gap
recorded earlier in this session.

## Documentation Status

- ADR: done — `docs/decisions/055-first-run-onboarding-wizard.md` (includes the
  keyboard/activation-ordering rationale).
- Vault: done — `memory/services/desktop.md` (onboarding entries + keyboard
  behaviour + fixed stale `SilentModelDownloadService` row + Application
  category), `memory/decisions/_index.md` (055 row),
  `memory/architecture/subsystems.md` (Onboarding Tour + Localization).
- Knowledge: done — [[avalonia-click-event-vs-command]],
  [[avalonia-window-activation-focus]] + index rows.
- Plans: folder completed, `INDEX.md` row moved to Completed.
- CHANGELOG: **deliberately not touched** — per ADR-054 the `/release-notes`
  skill drafts the version section from the tag range and its ADRs at release
  time. See Next Action.

## Next Action

1. Review and merge [PR #14](https://github.com/mdemin729/parlotype/pull/14).
2. **Before the next release**, run `/release-notes`: the tour is a headline
   user-facing feature and `CHANGELOG.md` currently has an empty
   `## [Unreleased]`. Tagging without a matching section fails the release
   workflow by design (ADR-054), so this is a hard precondition, not a nicety.
3. Optional follow-up the ADR records as a known limitation: the wizard
   repositions only on step change, not while the user drags the target window
   mid-step.
