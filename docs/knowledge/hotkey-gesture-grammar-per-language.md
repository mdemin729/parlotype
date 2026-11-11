---
title: "Hotkey gesture strings compose three formats: grammar per language"
area: "localization"
summary: "The modifier-side word sits mid-phrase (lowercase, in the case the gesture verb governs), and AlreadyBound quotes an imperative gesture used as subject."
verified: 2026-11-11
---

# Hotkey gesture strings

`HotkeyText.Gesture` builds `Gesture_HoldFormat`/`DoubleTapFormat` around `Modifier_Left/RightFormat`. The result then reappears in `Line`, `Hotkey_Hint_*` and `Conflict_AlreadyBoundFormat`.

- The side word sits mid-phrase: **lowercase**, in the case the gesture verb governs (Greek genitive `δεξιού`, Lithuanian accusative `dešinįjį`, Bulgarian definite `десния`). Slavic masculine-inanimate adjectives need only lowercase; ru/uk/pl already used the genitive via a noun head (`Удержание правого Ctrl`).
- Only `AlreadyBound` uses a modifier gesture as a *subject* (Reserved/ParameterHints/AltGr are chord-only — `HotkeyConflictDetector` returns before reaching them). An imperative or `Label: key` gesture cannot be a subject, so those locales quote it (`„{0}“ …`). Infinitive/noun gestures (cs, de, es, fr, pt, ru, uk, pl) read fine unquoted.
- `LocalizationParityTests.ModifierSide_IsLowercase_UnlessTheGestureLeadsWithIt` guards the capitalization; German is exempt because both gesture formats begin with `{0}`.
- Maltese `tal-Lemin` / `tax-Xellug` stay lowercase after the hyphen; the guard test caught a capitalised form.
- Not caught by tooling: a case error (el, lt) — only reading the rendered string finds it.

Per-language decisions: `docs/localization/glossaries/glossary-<c>.md`, section "Hotkey gesture grammar".

## Open

- Native-speaker review of the el/lt/bg case forms is still pending.
- Maintainer decision pending on two neutral-file options: quote `{0}` in
  `Settings_Hotkeys_Conflict_AlreadyBoundFormat`, and per-side composite keys.
