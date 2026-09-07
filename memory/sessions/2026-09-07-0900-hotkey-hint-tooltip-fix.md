---
title: "Session: 2026-09-07 — The tooltip nobody had wired through yet"
type: session
status: complete
tags: [localization, hotkeys, adr-064]
created: 2026-09-07
summary: "User reported (with a screenshot) that the record-button tooltip stays English under a Russian interface. HotkeyHint.Describe (Core) built the whole sentence unconditionally — a gap the previous session's amendment had explicitly documented as deliberate, since it's neither a conflict nor an error message. Fixed with the same Core-exposes-data / Desktop-words-it split, plus a HotkeyCoordinator subscription since the tooltip is pushed into the view model rather than bound."
---

# Session: 2026-09-07 — The tooltip nobody had wired through yet

## Active Focus

`src/Parlotype.Core/Hotkeys/HotkeyHint.cs`, `src/Parlotype.Desktop/ViewModels/Settings/HotkeyText.cs`,
`src/Parlotype.Desktop/Services/HotkeyCoordinator.cs`, `src/Parlotype.Desktop/Onboarding/OnboardingStepFactory.cs`.

## Decisions Made

- **`HotkeyHint.SelectPrimary(bindings)`** (Core) — new method exposing which binding the
  hint should describe (`DictationHotkey?`), factored out of `Describe`'s existing selection
  logic. `Describe` itself is unchanged in behavior and stays as the invariant English form
  the two `HotkeyHintTests` pin — grep-ability for a future log use, same rationale as
  `HotkeyGesture.DisplayString` — even though nothing reads it for the UI any more.
- **`HotkeyText.Hint(bindings)`** (Desktop) — the localized sentence, reusing
  `HotkeyText.Gesture` for the already-localized gesture phrase and three new
  `Hotkey_Hint_*` formats for the surrounding words. Both consumers
  (`HotkeyCoordinator.RefreshHotkeyHint`, `OnboardingStepFactory`'s recap step)
  switched from `HotkeyHint.Describe` to this.
- **`HotkeyCoordinator` gained a `Localizer.CultureChanged` subscription** (and matching
  unsubscribe in `Dispose`) — the tooltip is *pushed* into `TranscribeViewModel.HotkeyHintText`
  via `SetHotkeyHint`, not read through a binding, so nothing else would have told it to
  recompute on a live language switch. This is the same category of gap the previous
  session fixed six times (a value that recomputes correctly but nothing re-triggers it).

## Facts Learned

- Nothing new — this confirmed the previous session's own documented gap
  (`memory/architecture/subsystems.md` explicitly listed `HotkeyHint` under "still English
  on purpose") was in fact a real omission worth closing once a user hit it, not a
  correct scope boundary.

## Open Blockers

None. Both fixes (wording, and the live-switch subscription) are regression-tested and each
verified independently to fail against a targeted revert of only its own change before being
trusted. No manual run in the app — verified by test, consistent with how this whole
localization effort has been verified across sessions.

## Documentation Status

- ADR: done — third amendment to `docs/decisions/064-ui-localization-foundation.md`.
- Vault: done — `memory/architecture/subsystems.md`'s hotkey section and "still English on
  purpose" paragraph updated; `memory/services/core.md`'s `HotkeyHint` entry updated;
  `memory/decisions/_index.md`'s ADR-064 row got a short summary.
- Knowledge: none needed — no new non-derivable fact, just closing a documented gap with the
  established fix pattern.

## Next Action

Closed. The broader UI localization plan remains `in_progress` on the same optional phase-6
polish noted in the prior two sessions (pseudo-locale, ICU speech-language names,
`docs/localization.md`, a website note) — unchanged by this fix.
