---
title: Hotkey gesture strings compose three formats — grammar per language
type: knowledge
tags: [localization, hotkeys, grammar]
created: 2026-10-03
summary: The side word is mid-phrase (lowercase, case set by the gesture verb); an imperative gesture cannot be a sentence subject, so AlreadyBound quotes it
---

# Hotkey gesture strings

`HotkeyText.Gesture` builds `Gesture_HoldFormat`/`DoubleTapFormat` around `Modifier_Left/RightFormat`. The result then reappears in `Line`, `Hotkey_Hint_*` and `Conflict_AlreadyBoundFormat`.

- The side word sits mid-phrase: **lowercase**, in the case the gesture verb governs (Greek genitive `δεξιού`, Lithuanian accusative `dešinįjį`, Bulgarian definite `десния`). Slavic masculine-inanimate adjectives need only lowercase; ru/uk/pl already used the genitive via a noun head (`Удержание правого Ctrl`).
- Only `AlreadyBound` uses a modifier gesture as a *subject* (Reserved/ParameterHints/AltGr are chord-only — `HotkeyConflictDetector` returns before reaching them). An imperative or `Label: key` gesture cannot be a subject, so those locales quote it (`„{0}“ …`). Infinitive/noun gestures (cs, de, es, fr, pt, ru, uk, pl) read fine unquoted.
- `LocalizationParityTests.ModifierSide_IsLowercase_UnlessTheGestureLeadsWithIt` guards the capitalization; German is exempt because both gesture formats begin with `{0}`.
- Not caught by tooling: a case error (el, lt) — only reading the rendered string finds it.

Per-language decisions: `plans/2026-09-17-twenty-five-ui-languages/glossary-<c>.md`, section "Hotkey gesture grammar".
