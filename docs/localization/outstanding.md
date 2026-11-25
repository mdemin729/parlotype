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

- **macOS strings (ADR-078): review against a real localized Mac.** The seventeen keys
  added for macOS name system UI (System Settings, Privacy & Security, Accessibility,
  Login Items, the Applications folder, the menu bar). The names came from Apple's
  localized user guides and, for Czech, Slovak and Croatian, from the system resources of
  a macOS 13 machine; nobody has read them on a Mac running in that language. Each
  `glossary-<culture>.md` has a "macOS terms" table recording what was chosen. Raised by
  the translators:
  - **"Login Items" was renamed in macOS 15** ("Login Items & Extensions"). In English,
    Russian, Croatian and the Romance languages the old name is a prefix of the new one.
    In **Polish, Ukrainian, Czech and Slovak it is not** (pl "Logowanie" became "Rzeczy i
    rozszerzenia otwierane podczas logowania"). Polish and Ukrainian ship the macOS 13–14
    name, Czech and Slovak the macOS 15 one, Slovenian the macOS 15 one. Pick one target
    version and align them; only `Settings_Startup_WhatChangesBody_Mac` is affected.
  - **Languages macOS is not localized into** (Estonian, Maltese, Lithuanian, Latvian,
    Bulgarian) keep the English names in the Settings path, since that is what those
    users see on screen. Slovenian uses Slovenian names, which only macOS 15 and later
    show.
  - **"Launch agent"** has no localized Apple term in most languages and was coined in
    each; the `~/Library/LaunchAgents` path beside it identifies it.
  - **Portuguese follows European macOS** ("Definições do Sistema"); a Brazilian Mac says
    "Ajustes do Sistema". Same pt/pt-BR question the rest of the file already has.
  - **Dutch** `Dialog_AccessibilityAccess_Title` reads clumsily ("toegang tot
    Toegankelijkheid"); **Hungarian**'s is 1.6 times the English and may clip; **French**
    `Common_NotNow` is "Pas maintenant" where Apple's own button says "Plus tard".
  - **Maltese** new dialog titles are sentence case while its older ones are Title Case.
- **Maltese `Onboarding_Tray_Body` lost its paragraph break** (two spaces where the
  English has a blank line). Found while adding the macOS variant, which has the break.
  Fix through the import pipeline.
- **The microphone usage sentence in `packaging/macos/Info.plist` is English only.**
  macOS shows it in its own permission prompt. Localizing it means an `InfoPlist.strings`
  per language, which should be generated from resx so it stays inside the pipeline.

## Deliberately dropped

- **Pseudo-locale (`qps-ploc`).** Planned in the original ui-localization plan as a way to
  catch untranslated stragglers and truncation. Never built, and now dropped: the
  hardcoded-literal scan in `scripts/check-localization.ps1`, resx parity tests and the
  headless layout renders in every language cover the same ground.
