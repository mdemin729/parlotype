---
title: "Session: 2026-10-03 hotkey gesture grammar"
type: session
status: active
tags: [localization, hotkeys]
created: 2026-10-03
summary: Audited hotkey gesture strings across 24 locales; fixed side-word case/capitalization and AlreadyBound sentences via the import pipeline
---

# Session: 2026-10-03

## Active Focus
Gesture captions built by `HotkeyText` ("Hold Right Ctrl") in the 24 satellite locales.

## Decisions Made
- Translation-only fix: lowercase side words in 14 locales, genitive in el, accusative in lt, definite form in bg; `AlreadyBound` quoted/reworded in 15 (+ fr, pt, it mode slot). No neutral-file change.
- Added `ModifierSide_IsLowercase_UnlessTheGestureLeadsWithIt` and `HotkeyText_PutsTheSideInTheCaseTheGestureVerbGoverns`.

## Facts Learned
- See `memory/knowledge/hotkey-gesture-grammar-per-language.md`.
- The new test also caught Maltese `tal-Lemin` / `tax-Xellug` (should be lowercase after the hyphen).

## Open Blockers
- Native-speaker review of el/lt/bg case forms is still pending. Optional neutral change proposed to the maintainer: quote `{0}` in `Conflict_AlreadyBoundFormat`.

## Documentation Status
- ADR: none required
- Vault (services/architecture): none required
- Knowledge (non-derivable facts): done — `memory/knowledge/hotkey-gesture-grammar-per-language.md`

## Next Action
Maintainer decision on the neutral-file options (quoted `{0}` in AlreadyBound; per-side composite keys).
