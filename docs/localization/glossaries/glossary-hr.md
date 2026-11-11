---
title: Croatian (hr) translation glossary
status: reference
created: 2026-09-18
---

# Croatian (hr) glossary

Companion to `hr.json` (389/389 keys, assembled from two slices). Read `brief.md` first —
this file records the choices specific to Croatian. The Czech and Polish glossaries were
read only for their *structural* solution to the placeholder/case problem — Croatian
computing vocabulary is its own choice throughout (e.g. **računalo**, **sučelje**,
**poslužitelj**, **preuzimanje**, not internationalisms, and deliberately not the Serbian
equivalents a general model tends to drift toward). No coordination with the parallel
Slovenian translation — the two are separate languages with separate vocabulary decisions.

## Register

- **Formal address, plural imperative verb forms ("Vi" register), no explicit pronoun.**
  Body and description text uses the formal imperative/verb form Croatian commercial
  software and Windows itself use — "Odaberite…", "Pritisnite…", "Otvorite…" — never the
  informal singular ("ti") form. This matches Microsoft's own Croatian Windows/Office
  localization convention.
- **Buttons and short commands use the plain 2nd-person-singular imperative**, matching
  Windows' own Croatian button style: "Spremi", "Zatvori", "Otvori", "Preuzmi", "Izbriši",
  "Dodaj", "Ukloni", "Promijeni", "Pregledaj…". This is standard Croatian UI convention —
  Croatian has no infinitive-as-button convention the way Czech does, so the short
  imperative singular is the natural equivalent.
- Sentence case throughout, no exclamation marks, no marketing voice — matches the brief.
- Past-participle agreement defaults to **neuter singular** wherever the logical subject
  is a phrase rather than a single noun of known gender (e.g.
  `Settings_Hotkeys_Conflict_ReservedFormat` → "je rezervirano", not a gendered form) —
  the normal Croatian default for an impersonal or unclear-gender subject.
- Key names, keyboard-key loanwords (`Ctrl`, `Alt`, `Shift`) are treated as grammatically
  masculine by default when an adjective must agree with them ("Lijevi {0}", "Desni {0}")
  — the common pattern for consonant-ending technical loanwords in Croatian, and it means
  the choice never depends on which key name actually fills the slot.

## Quotation convention

Croatian double quotes: **„…”** — opening U+201E (double low-9 quotation mark), closing
U+201D (right double quotation mark, the same shape as an English closing smart quote).
Example: `„Large v3 Turbo”`. Never the straight `"…"` and never guillemets (those are a
secondary/nested style in Croatian typography, not used here).

The ADR-032 slogan is kept in English inside these quotes, as a citation of the ADR's own
title: `(ADR-032, „Local by default. Cloud by choice.”)`.

## Dash convention

Croatian typography does not use the em dash. Every em dash `—` in the English source was
converted to an **en dash `–` with a space on each side** for a parenthetical break — e.g.
`Onboarding_Recording_EscLine`: "Esc – otkazuje trenutno snimanje bez upisivanja ičega."
`Settings_Hotkeys_LineFormat` (`"{0} — {1}"`) becomes `"{0} – {1}"` for the same reason.

## Non-breaking space

`Settings_SilenceTimeout_WaitFormat` → `"{0} ({1} s)"` with U+00A0 between `{1}` and the
unit `s`, so the number and its unit can't wrap apart — Croatian follows the same SI
spacing convention. No other key needed one — every other pre-formatted size/version
string (`"1.5 GB"`, `"0.4.4"`) arrives as opaque `{1}` data whose internal spacing isn't
mine to control.

## Core glossary

| Term | Croatian | Notes |
|---|---|---|
| dictation | **diktiranje** | verb: diktirati |
| transcribe / transcription | **prepisivati** / **prepisivanje** | native Slavic root ("write down what is spoken"), deliberately not the internationalism "transkribirati" — kept distinct from *prijevod* throughout |
| translate / translation | **prevoditi** / **prijevod** | never used for transcribe; surface form varies noun vs. imperative by UI role (toggle label "Prijevod" vs. header "Prevedi na") but the root is constant |
| recording (live capture) | **snimanje** | |
| speech engine | **govorni modul** (short: **modul**) | "engine" has no native one-word Croatian computing term in general use; "modul" is the established short form |
| model | **model** | already a Croatian word, unchanged |
| runtime | **izvršno okruženje** (short: **okruženje**) | "Whisper runtime" → "Whisper okruženje"; also covers `Settings_Runtime_Description`'s "backend" — one concept, one word |
| source language | **izvorni jezik** | |
| target language | **ciljni jezik** | |
| hotkey | **tipkovnički prečac** | matches Croatian Windows' own term for keyboard shortcut |
| push-to-talk | **držanje** (mid-sentence noun) / **Drži i govori** (badge) | badge phrased as an imperative pair, matching the two-word English badge; the lowercase in-sentence form is a plain noun |
| toggle (mode) | **preklop** | badge and mid-sentence noun are the same word, capitalization only differs |
| binding | **dodjela** (the act of binding) / **prečac** (the item itself) | no key needs a term distinct from these two |
| tray | **područje obavijesti** | official Windows Croatian term for the notification area |
| widget | **widget** | kept as an established Croatian IT loanword, declines regularly (widgeta, widgetu) |
| waveform | *(no key in this set surfaces this word alone)* | would be "valni oblik" if needed later |
| wait time / silence timeout | **vrijeme tišine** | `Settings_SilenceTimeout_Title`; Medium/Long/Extended/Very Long are neuter adjectives agreeing with "vrijeme" (Srednje, Dugo, Produljeno, Vrlo dugo) |
| punctuation | **interpunkcija** | |
| profanity filter | **filtar psovki** | |
| inject / typed into | **upisivati se u** | "the text is typed into the app" → "tekst se upisuje u…"; no invented technical term, and deliberately not "umetnuti" (insert/paste), to avoid confusion with the clipboard |
| clipboard | **međuspremnik** | standard Croatian Windows term |
| cloud engine / cloud provider | **modul u oblaku** / **pružatelj usluge u oblaku** | "oblak" is established Croatian IT vocabulary (Microsoft 365 Croatian uses it for cloud storage/services) — used natively rather than keeping the English loanword "cloud" |
| Cloud (badge) | **Oblak** | one word, short, native — chosen over keeping "Cloud" untranslated, since "oblak" is genuine Croatian vocabulary and reads more naturally in a tight badge |
| API key | **API ključ** | |
| onboarding / tour | **vodič** | "Parlotype tour" → "Vodič kroz Parlotype" |
| restart required | **potrebno ponovno pokretanje** | |
| build (software) | **inačica** | distinct from **verzija** (version number, e.g. "0.4.4"); "build" (a compiled llama-server artifact, "development build") uses inačica, "version" (semver) uses verzija |
| prompt (LLM instruction) | **prompt** / **promptovi** | kept as the loanword Croatian AI-tooling users already use, matching the cs/pl precedent, rather than a native coinage |
| backend | **okruženje** | folded into *runtime* rather than kept as a separate word — one concept, one word |

## Case-inflection notes — keys where the raw `{0}` would force an oblique Croatian case

Croatian has seven cases, and past-participle forms and adjectives also carry gender.
Every key below substitutes a raw, un-inflectable value (an engine, runtime, or language
name) into a slot where the literal phrasing would demand a case ending or a
gender-agreeing form the raw string cannot supply. Each was restructured so the slot sits
after a colon, as a nominative apposition, or behind a gender-invariant present-tense
verb — flagging every one here per the brief's instruction, since other languages
converging on the same keys independently is a signal for the maintainer, not a
per-language quirk:

- **`Transcribe_Status_RuntimeRestartRequiredFormat`** — "use the {0} runtime" would need
  the accusative of "okruženje" governed by "koristiti", with {0} as an adjectival
  modifier that a raw, undeclined name cannot become. Restructured behind a colon:
  `"Ponovno pokrenite Parlotype da biste upotrijebili ovo okruženje: {0}"`.
- **`Settings_Runtime_RestartNoteFormat`** — two placeholders, "running the {0} runtime"
  and "switch to {1}". Same problem twice over. Restructured both behind colons:
  `"Ova sesija već radi u ovom okruženju: {0}. Whisper bira okruženje samo jednom po
  pokretanju, stoga ponovno pokrenite Parlotype kako biste prešli na ovo okruženje: {1} –
  dotad snimanje neće započeti."`
- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — a literal "Prevedi
  na {0}" needs the accusative of the language name after "na", which a raw, undeclined
  value cannot reliably supply once language names are localized. Restructured to drop
  the preposition entirely: `"Prijevod: {0}"`.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Croatian *can*
  say "Govorim engleski" for many language names (accusative = nominative for the
  adjectival-noun language names), but that only holds for the closed set of names
  currently shipped and breaks once arbitrary localized names arrive. Restructured
  defensively, matching every other language that flagged this key: `"Govorite: {0} →
  Parlotype upisuje: {1}."`
- **`Language_Toast_SourceUnsupportedFormat`** ("{0} isn't a source in {1}.") — "in {1}"
  needs the locative of the engine name ("u Whisperu"), which breaks for an indeclinable
  name like "Gemma 4", and the predicate "isn't a source" would also need gender
  agreement with an unknown {0}. Added a classifying noun and a colon-guarded apposition:
  `"Jezik {0} nije dostupan kao izvorni u ovom modulu: {1}. Koristi se raspored
  tipkovnice."` — "Jezik" also fixes the gender-agreement risk on {0}, since "Jezik" is a
  known masculine noun the sentence can agree with instead of the raw value.
- **`Language_Toast_TargetUnsupportedFormat`** ("{0} isn't available in {1}. Translation
  set to {2}.") — same locative problem on {1}, plus {2} sitting after "to" (accusative).
  Same fix: `"Jezik {0} nije dostupan u ovom modulu: {1}. Jezik prijevoda: {2}."`
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — the passive "not
  supported by X" needs the instrumental case on the agent. Flipped to active voice so
  {0} becomes a nominative apposition to "modul" instead: `"Prethodni ciljni jezik je
  resetiran – modul {0} ga ne podržava."` A bare "modul" + name apposition in the
  nominative needs no case ending regardless of what the name turns out to be.
- **`Cloud_Error_ProviderUnavailableFormat`** ("{0} is unavailable right now…") — the
  only `Cloud_Error_*`/`Cloud_NotConfigured_*` key that does **not** already put {0}
  ahead of a colon (see `Cloud_ProviderName_*`'s own comment, which asks for exactly that
  pattern). A literal adjective ("nedostupan/-na/-no") would force gender agreement
  Croatian cannot resolve for an indeclinable foreign provider name. Rewrote with an
  invariant present-tense verb instead of an adjective: `"{0} trenutačno ne odgovara
  (HTTP {1}) – pokušajte ponovno za koji trenutak. ({2})"` — present-tense Croatian verbs
  never mark gender, so "ne odgovara" is safe regardless of the provider name's gender.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to {1}.") —
  {0} is a whole gesture phrase, not a single-gendered noun, so the past participle
  defaults to neuter ("dodijeljeno"); {1} is a mode name that would need the dative case
  after "already bound to" if inserted directly. Added a classifying noun and a colon:
  `"{0} je već dodijeljeno načinu rada: {1}."`

Keys I did **not** flag despite carrying a name in a placeholder: anywhere the value is
quoted (`„{0}”` — a quoted citation is conventionally left undeclined in Croatian UI copy,
same trick cs/pl use with `„{0}“`/`„{0}”`), anywhere the value is a bare nominative
subject followed by an invariant present-tense verb (`Language_UnavailableNoteFormat`,
`Language_Toast_EngineCannotTranslateFormat` — Croatian present-tense verbs never mark
gender, so a raw subject in front of one is always safe), `Transcribe_Status_
RuntimeUnavailableFormat` (rephrased with "Okruženje {0}" as a nominative apposition,
identical trick to the Language_Toast fixes above but needed no separate flag since it
was already naturally colon-safe), the hold/double-tap key-name formats
(`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`) — phrased as **verb +
accusative object** ("Drži {0}", "Dvaput pritisni {0}"), and since all protected key names
(`Ctrl`, `Alt`, `Shift`, `Win`, `Space`) default to masculine-inanimate gender in Croatian,
their accusative singular equals the nominative, so `{0}` never visibly declines — and
`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat` ("Lijevi {0}" / "Desni {0}"), where
the same masculine-inanimate default means the adjective never needs to change form no
matter which key name fills the slot.

## Windows-terminology matches

Used the actual Croatian Windows 10/11 UI wording rather than a literal rendering for:

- Task Manager → **Upravitelj zadataka**; its Startup apps tab → **Aplikacije pri
  pokretanju**; "Enable" → **Omogući** (`Settings_Startup_BlockedBody`,
  `Settings_Startup_WhatChangesBody`, `Settings_Startup_State_BlockedByWindows`).
- Task View → **Prikaz zadataka**; the Run dialog is rendered as "dijaloški okvir
  „Pokreni”", quoting the dialog's own Croatian title the way Windows itself shows it;
  Windows Voice Typing → **Windows glasovno pisanje**.
- `Settings_Category_Audio` → **Zvuk**, matching the Windows Sound settings page name.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — I could not verify a single
   fixed official Croatian name for this menu from memory; used "Izbornik brzih
   poveznica" as a literal, readable rendering. Flagged for a native check.
2. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used the short "Projekcija"
   rather than a fuller descriptive phrase; worth checking against the exact wording of
   the current Windows quick-panel title if parity matters.
3. **`Settings_Category_Input`** ("Input") — translated as **Unos**. Generic enough to
   match the English's own vagueness (it groups the Hotkeys page), but exactly as
   underspecified in Croatian as in English.
4. **"prompt/promptovi" vs a native coinage** — chose the loanword to match how Croatian
   AI-tooling discussion already refers to this concept; a native alternative (e.g.
   "uputa") is defensible but reads stiffer given "Promptovi" is already the nav label.
5. **`Settings_Hotkeys_Conflict_ParameterHintsFormat`** — declined "Visual Studio" and
   "VS Code" into the locative ("u Visual Studiju i VS Codeu"), common in Croatian
   technical writing, rather than leaving the never-translate product names
   grammatically frozen. This is grammatical adaptation, not translation of the name
   itself (per the localization skill's "declension is not translation" guidance), but
   flagging the choice in case strict identifier parity is preferred.
6. **`Settings_Hotkeys_Reserved_GameBar`** — adapted with a Croatian genitive ending
   ("Otvaranje Game Bara") rather than leaving it fully undeclined; same reasoning as #5.
7. **Cloud badge as "Oblak" rather than keeping "Cloud"** — a deliberate departure from
   what several other shipped languages did (keeping the English word). Croatian "oblak"
   is genuine, already-established IT vocabulary rather than an invented native coinage,
   and the brief explicitly asks for Croatian computing vocabulary over internationalisms,
   so I judged the native word the better default here. Worth a second look if brand
   consistency with the other languages' badges turns out to matter more than idiom.

## Validation performed

Checked both slices against `hr.brief.part1.json`/`part2.json`: all 389 keys present
across the two output files with no key appearing in both, every `{0}`/`{1}`/`{2}`
placeholder set matches the English source per key, the literal tokens `{speech_lang}`
and `{text_lang}` in `Settings_Prompts_Help_BuiltInBody` are preserved verbatim and with
the same occurrence count as the English (`{speech_lang}` twice, `{text_lang}` once), and
no translated value over 25 characters is byte-identical to the English source.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Zadano (doslovno prepisivanje)"** — verbatim/word-for-word rendered as *doslovno (word-for-word)*, paired with the already-established *prepisivanje* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Drži desni Ctrl`.**

Side is now `desni {0}` / `lijevi {0}`, lowercase (accusative = nominative for masculine inanimate). `Conflict_AlreadyBoundFormat` quotes the imperative gesture: `„{0}” je već dodijeljeno načinu rada: {1}.`
