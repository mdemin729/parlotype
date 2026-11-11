# Localization: outstanding work and dropped ideas

Carried over from the plans that shipped the 25-language interface
([ADR-064](../decisions/064-ui-localization-foundation.md),
[ADR-069](../decisions/069-twenty-five-ui-languages.md)). The plan folders were removed
once the work landed; git history keeps them.

## Outstanding

- **Rephrase the three `Language_*` English strings.** Twelve languages restructured them
  while translating; Bulgarian and Romanian confirm the cause is case marking. Changing the
  neutral `Strings.resx` is a maintainer decision (see ADR-069). Polish currently ships a
  latent defect that only a change to the English can fix. Background in
  [research.md](research.md).
- **Native-speaker review**, Maltese first. All 24 translated files shipped as
  first-class, without a "machine translated" badge; review is follow-up, not a gate.
- **Drive the running app by hand in three languages.** The headless renders cover layout
  and the tests cover live switching, but nobody has clicked through the real app.

## Deliberately dropped

- **Pseudo-locale (`qps-ploc`).** Planned in the original ui-localization plan as a way to
  catch untranslated stragglers and truncation. Never built, and now dropped: the
  hardcoded-literal scan in `scripts/check-localization.ps1`, resx parity tests and the
  headless layout renders in every language cover the same ground.
