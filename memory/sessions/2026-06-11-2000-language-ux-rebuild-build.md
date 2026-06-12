---
title: "Session: 2026-06-11 — Language UX Rebuild (build, all phases)"
type: session
status: active
tags: [language, translation, keyboard-layout, popover, transcribe, adr-036]
created: 2026-06-11
summary: "Built the entire Language UX rebuild plan (P0 foundation, P1 Settings page, P2 Transcribe quick picker) plus ADR-036 and vault updates. Six commits on claude/sweet-mclean-93d200. All 657 tests green; screenshots verified for every spec state."
---

# Session: 2026-06-11 — Language UX Rebuild (build)

## Active Focus

Executed `plans/2026-06-08-language-ux-rebuild/implementation-plan.md` end to end
on autopilot, in the worktree branch `claude/sweet-mclean-93d200`:

- **P0 Core** (`f87c0e5`): `KeyboardLayoutCode` sentinel, `TranslationForm`
  enum on `LanguageCapabilities`, `IKeyboardLayoutService`/`KeyboardLayoutInfo`,
  pure `SourceLanguageResolver`, MRU sentinel exclusion.
- **P0 Platform** (`bd0ee2d`): `Win32KeyboardLayoutService` (P/Invoke,
  foreground-thread HKL) + `NoOpKeyboardLayoutService`; pipeline + Gemma prompt
  resolve the keyboard sentinel.
- **P0 Desktop** (`f74a3a8`): shared `LanguageRelationshipViewModel` (spec §7
  state machine + §8 fallback toasts) + 24 tests.
- **P1 page** (`a71c313`): popover pickers (rich rows, specials, Recent/All
  groups, >8 search rule), three-column layout with model-driven target forms,
  connector pill, summary, toast region; page VM slimmed to a wrapper.
- **P2 widget** (`ccbeb91`): Transcribe quick-picker strip + 268px flyout;
  shared derivations consolidated onto the relationship VM; recording-stop
  moved into `TranscribeViewModel` via `RelationshipChanged`.
- **Docs slice** (this commit): ADR-036 (supersedes ADR-035), vault service
  profiles + Language & Translation subsystem rewrite, two knowledge files,
  plan flipped to completed.

## Decisions Made

- Chip mirroring: while translation is off the Transcribe target chip mirrors
  the source ("Auto = Auto" reads "typed as spoken").
- Toggle-form forced-target reset is **silent when translation is off** (toast
  only for user-visible changes); startup reconciliation never toasts.
- Selecting a language in the full target picker also turns translation on;
  the "Off" row turns it off and keeps the resting target.
- Picker chrome extracted to a shared `Border.popoverChrome` app style; the
  picker `UserControl` is chrome-free so page (300px) and flyout (268px) size it.
- Default source stays `auto` for fresh installs — keyboard is pinned first in
  the picker but not forced as default (spec didn't mandate changing it).
- Language tiles show the upper-cased ISO code instead of a globe glyph
  (informative + font-safe).

## Facts Learned

- Win32 keyboard layouts are per-thread; transient LANGIDs throw
  `CultureNotFoundException` → [[../knowledge/win32-keyboard-layout]].
- Headless `CaptureRenderedFrame` excludes the popup layer; `DataContext` swap
  rebases sibling bindings; light dismiss eats the anchor click →
  [[../knowledge/avalonia-popup-patterns]].
- The screenshot report HTML is overwritten by whichever fixture class flushes
  last when classes share a report path (pre-existing; cosmetic).

## Open Blockers

None. Build zero-warning; 207 Desktop + 348 core/platform + 102 benchmark
tests green; app smoke-runs clean; dark+light screenshots inspected for all
§Spec 6/8 states and the three strip states.

## Documentation Status

- ADR-036 written; ADR-035 marked superseded (file + vault index).
- Vault: `services/core|platform|desktop.md`, `architecture/subsystems.md`,
  `decisions/_index.md` updated.
- Knowledge: 2 new entries + index rows.
- Plans: task.md completed; `plans/INDEX.md` moved to Completed.

## Next Action

Plan is fully delivered. Candidate follow-ups (not started): merge the branch
to master; manual on-Windows verification of the topmost-widget flyout above
other apps (plan item 19 was only smoke-checked — popup belongs to the topmost
window so it should inherit, but eyes-on confirmation is cheap); a future
cleanup PR removing the legacy `TranslateToEnglish`/`RecentLanguages` keys
(ADR-035 trade-off).
