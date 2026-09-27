---
title: Czech (cs) translation glossary
status: reference
created: 2026-09-18
---

# Czech (cs) glossary

Companion to `cs.json` (389/389 keys, assembled from two slices). Read `brief.md` first —
this file only records the choices specific to Czech. Translated from the English source;
Polish and Ukrainian glossaries were read for their *structural* solution to the
placeholder/case problem only, not for word choice — Czech IT vocabulary is its own
(e.g. **modul**, not **silnik**/**рушій**, for "engine"; **schránka**, not
**schowek**/**буфер обміну**, for clipboard).

## Register

- **Formal address (vykání), no explicit "vy" pronoun where avoidable.** Body and
  description text uses the formal second-person-plural imperative/verb form —
  "Zvolte…", "Stiskněte…", "Otevřete…" — never the informal "ty" (tykání). This matches
  how Czech Windows and virtually all Czech commercial desktop software addresses the
  user, and how the brief asks me to record a T/V choice and never mix it.
- **Buttons and short commands use the plain infinitive**, matching Windows' own button
  style: "Zavřít", "Uložit", "Otevřít", "Stáhnout", "Smazat", "Přidat", "Odebrat",
  "Změnit", "Obnovit", "Procházet…". This is standard Czech UI convention and sidesteps
  the tykání/vykání question entirely for that surface (an infinitive carries no person).
- Sentence case throughout, no exclamation marks, no marketing voice — matches the brief.
- Past-tense/participle agreement defaults to **neuter singular** wherever the logical
  subject is a phrase rather than a concrete noun of known gender (e.g.
  `Settings_Hotkeys_Conflict_ReservedFormat` → "je vyhrazeno", not a gendered form) —
  the normal Czech default for an impersonal or unclear-gender subject.

## Quotation convention

Czech low-high double quotes: **„…“** — opening U+201E (double low-9), closing U+201C
(left double quotation mark, used as the closing glyph in Czech typography). Example:
`„Large v3 Turbo“`. Never the straight `"…"` and never guillemets.

The ADR-032 slogan is kept in English inside these quotes, as a citation of the ADR's own
title: `(ADR-032, „Local by default. Cloud by choice.“)`.

## Dash convention

Czech prefers the **en dash `–`** with a space on each side for a parenthetical break,
not the em dash `—` the English source uses. I converted every such em dash to `–` —
e.g. `Onboarding_Recording_EscLine`: "Esc – zruší aktuální nahrávání, aniž by se cokoli
napsalo." `Settings_Hotkeys_LineFormat` (`"{0} — {1}"`) becomes `"{0} – {1}"` for the
same reason.

## Non-breaking space

`Settings_SilenceTimeout_WaitFormat` → `"{0} ({1} s)"` with U+00A0 between `{1}` and the
unit `s`, tying the number to its unit so it cannot wrap. No other key needed one — every
other pre-formatted size/version string (`"1.5 GB"`, `"0.4.4"`) arrives as opaque `{1}`
data I don't control the internal spacing of.

## Core glossary

| Term | Czech | Notes |
|---|---|---|
| dictation | **diktování** | verb: diktovat |
| transcribe / transcription | **přepisovat** / **přepis** | kept distinct from překlad throughout |
| translate / translation | **překládat** / **překlad** | never used for transcribe |
| recording (live capture) | **nahrávání** | `Transcribe_Status_Recording` → "Nahrávání…" |
| speech engine | **hlasový modul** (short: **modul**) | short form used once page context is clear, mirrors the pl/uk `silnik`/`рушій` short-form pattern with the same root word |
| model | **model** | already a Czech word, unchanged |
| runtime | **prostředí** | "Vulkan runtime" → "prostředí Vulkan"; also reused for `Settings_Runtime_Description`'s "backend" — one concept, one word |
| source language | **zdrojový jazyk** | |
| target language | **cílový jazyk** | |
| hotkey | **klávesová zkratka** | matches Windows' own term |
| push-to-talk | **podržení** | noun (neuter, -í stem); pairs with "Podržet {0}" for the gesture format |
| toggle (mode) | **přepínání** | noun (neuter, -í stem) |
| binding | *(not surfaced as a standalone word in this key set)* | no key needs a distinct term for "binding" separate from "klávesová zkratka" |
| tray | **oznamovací oblast** | official Windows Czech term for the system tray/notification area |
| widget | **widget** | kept as the established Czech IT loanword (unchanged spelling, same as several other shipped languages) |
| waveform | *(no key in this set surfaces this word alone)* | would be "křivka" / "vlnový průběh" if needed later |
| wait time / silence timeout | **prodleva ticha** | `Settings_SilenceTimeout_Title`; the Medium/Long/Extended/Very Long options are feminine adjectives agreeing with "prodleva" (Střední, Dlouhá, Prodloužená, Velmi dlouhá) |
| punctuation | **interpunkce** | |
| profanity filter | **filtr vulgarismů** | |
| inject / typed into | **psát / napsat** | "the text is typed into the app" → "text se napíše do…"; no invented technical term, and deliberately not "vložit" (paste), to avoid confusion with the clipboard |
| clipboard | **schránka** | standard Czech Windows term |
| cloud engine / cloud provider | **cloudový modul** / **cloudový poskytovatel** | |
| API key | **klíč API** | genitive-style compound, matches Microsoft's own Czech style guide over the calque "API klíč" |
| onboarding | **prohlídka** | "Parlotype tour" → "Prohlídka Parlotype" |
| restart required | **vyžadován restart** / heading: **Nutný restart** | |
| build (software) | **sestavení** | "development build" → "vývojové sestavení"; also used for llama-server builds |
| prompt (LLM instruction) | **prompt** / **prompty** | kept as the loanword Czech AI-tooling users already use, matching the pl/uk precedent, rather than a native coinage |
| backend | **prostředí** | folded into the *runtime* term rather than kept as a separate loanword — `Settings_Runtime_Description`'s "Whisper.net backend" is the same concept as every other `Settings_Runtime_*` key |
| Cloud (badge) | **Cloud** | kept untranslated, like `de`/`fr`/`it`/`nl` — Czech tech usage says "cloud" natively; no shorter native word reads as cleanly in a tight badge |

## Case-inflection notes — keys where the raw `{0}` would force an oblique Czech case

Czech has seven cases, and past-tense/participle forms and some adjectives also carry
gender. Every key below substitutes a raw, un-inflectable value (an engine, runtime or
language name) into a slot where the literal phrasing would demand a case ending or a
gender-agreeing form the raw string cannot supply. Each was restructured so the slot
sits after a colon, as a nominative apposition, or behind an invariant verb — flagging
every one here per the brief's instruction, since two or more independent languages
converging on the same keys is a signal for the maintainer, not a per-language quirk:

- **`Transcribe_Status_RuntimeRestartRequiredFormat`** — "use the {0} runtime" would put
  {0} in the accusative governed by "použít prostředí X" as one NP, close enough to a
  fixed apposition that it is *probably* safe on its own (neuter "prostředí" doesn't
  visibly decline), but I colon-guarded it anyway for consistency with the next key and
  because the pattern generalizes better if a future engine name needs real inflection:
  `"Restartujte Parlotype, chcete-li použít prostředí: {0}"`.
- **`Settings_Runtime_RestartNoteFormat`** — two placeholders, "running the {0} runtime"
  and "switch to {1}". Same apposition risk as above, doubled, plus {1} sits after "na"
  (accusative) with no natural apposition to lean on. Restructured both behind colons:
  `"Tato relace už běží v prostředí: {0}. Whisper volí prostředí jen jednou za spuštění,
  restartujte proto Parlotype, chcete-li přepnout na prostředí: {1} – do té doby se
  nahrávání nespustí."`
- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — a literal "Přeložit
  do {0}" needs the genitive of the language name after "do", which a raw, undeclined
  value cannot supply once language names are localized. Restructured to drop the
  preposition entirely: `"Překlad: {0}"`.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — "Mluvíte {0}"
  is not idiomatic Czech for "you speak Spanish" (Czech wants an adverb, "mluvíte
  španělsky", which a raw noun-form language name cannot become), and even a
  preposition-based rescue ("mluvíte jazykem {0}") forces the instrumental. Restructured
  around colons instead: `"Mluvíte: {0} → Parlotype píše: {1}."`
- **`Language_Toast_SourceUnsupportedFormat`** ("{0} isn't a source in {1}.") — "in {1}"
  needs the locative of the engine name ("ve Whisperu"), which breaks for an
  undeclinable name like "Gemma 4", and the predicate "isn't a source" would also need
  gender agreement with an unknowable {0}. Rewrote with an invariant impersonal verb
  and a colon-guarded apposition: `"{0} nelze použít jako zdrojový jazyk v modulu: {1}.
  Použije se rozložení klávesnice."` — "nelze použít" doesn't inflect for gender in any
  Czech tense, so {0}'s unknown gender stops being a problem.
- **`Language_Toast_TargetUnsupportedFormat`** ("{0} isn't available in {1}. Translation
  set to {2}.") — same locative problem on {1} as above, plus a gender-agreement risk on
  "available", plus {2} sitting after "to" (accusative). Rewrote with the same invariant
  verb and apposition pattern, and moved {2} behind a label: `"{0} nelze použít v
  modulu: {1}. Jazyk překladu: {2}."`
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — the passive "not
  supported by X" needs the instrumental case on the agent ("modulem {0}"). Flipped to
  active voice so {0} becomes the nominative subject instead: `"Předchozí cílový jazyk
  byl resetován – modul {0} ho nepodporuje."` A bare "modul" + name apposition in the
  nominative needs no case ending regardless of what the name turns out to be.
- **`Cloud_Error_ProviderUnavailableFormat`** ("{0} is unavailable right now…") — the
  only `Cloud_Error_*`/`Cloud_NotConfigured_*` key that does **not** already put {0}
  ahead of a colon (see `Cloud_ProviderName_*`'s own comment, which asks for exactly
  that pattern). A literal adjective ("nedostupný/-á/-é") would force gender agreement
  Czech cannot resolve for an indeclinable foreign provider name. Rewrote with an
  invariant present-tense verb instead of an adjective: `"{0} teď neodpovídá (HTTP {1})
  – zkuste to za chvíli znovu. ({2})"` — "neodpovídá" carries no gender in the present
  tense, so it is safe regardless of the provider name's grammatical gender.

Keys I did **not** flag despite carrying a name in a placeholder: anywhere the value is
quoted (`„{0}“` — a quoted citation is conventionally left undeclined in Czech UI copy,
same trick pl/uk use with `„{0}”`/`«{0}»`), anywhere the value is a bare nominative
subject followed by an invariant verb (`Language_UnavailableNoteFormat`,
`Language_Toast_EngineCannotTranslateFormat` — present-tense Czech verbs never mark
gender, so a raw subject in front of one is always safe), the hold/double-tap key-name
formats (`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`,
`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat`) — I deliberately phrased these as
**verb + accusative object** ("Podržet {0}", "Dvakrát stisknout {0}") rather than
**noun + genitive** ("stisk {0}"), and for masculine inanimate nouns/adjectives
(`Ctrl`, `pravý`, `levý`) the accusative singular is identical to the nominative, so
`{0}` never visibly declines — and `Settings_Hotkeys_Conflict_Mode_PushToTalk`/`_Toggle`
("podržení"/"přepínání"), whose neuter -í stems keep an identical form across
nominative, genitive, dative, accusative and locative singular (only the instrumental
differs), so slotting either into `Settings_Hotkeys_Conflict_AlreadyBoundFormat`'s
dative-governing "přiřazeno k {1}" needed no rephrasing at all.

## Windows-terminology matches

Used the actual Czech Windows 10/11 UI wording rather than a literal rendering for:

- Task Manager → **Správce úloh**; its Startup tab → **Po spuštění**; "Enable" → **Povolit**
  (`Settings_Startup_BlockedBody`, `Settings_Startup_WhatChangesBody`,
  `Settings_Startup_State_BlockedByWindows`).
- File Explorer → **Průzkumník souborů**; the Run dialog → **Spustit** (rendered as
  "dialogové okno Spustit", quoting the dialog's own Czech title the way Windows itself
  shows it); Task View → **Zobrazení úkolů**; Game Bar → **Herní panel**; Windows Voice
  Typing → **Hlasové psaní Windows**.
- `Settings_Category_Audio` → **Zvuk**, matching the Windows Sound settings page name,
  reused verbatim for `Settings_LlamaCpp_AudioLabel`.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — I could not verify a single
   fixed official Czech name for this menu from memory; used "Nabídka Rychlé odkazy" as
   a literal, readable rendering. Flagged for a native check, same as the Polish and
   Ukrainian files flag it.
2. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used the short "Projekce"
   rather than a fuller descriptive phrase; worth checking against the exact wording of
   the current Windows quick-panel title if parity matters.
3. **`Settings_Category_Input`** ("Input") — translated as **Vstup**. Generic enough to
   match the English's own vagueness (it groups the Hotkeys page), but exactly as
   underspecified in Czech as it is in English — flagging it as the Polish glossary
   does, for the same reason.
4. **"prompt/prompty" vs a native coinage** — chose the loanword to match how Czech
   AI-tooling discussion already refers to this concept, avoiding collision with
   "nápověda"/tooltip vocabulary; a native alternative (e.g. "instrukce") is defensible
   but reads stiffer in a settings list that already uses "Prompty" as a nav label.
5. **`Settings_Hotkeys_Conflict_ParameterHintsFormat`** — I declined "Visual Studio" into
   the locative ("ve Visual Studiu"), which is very common in Czech technical writing,
   rather than leaving the never-translate product name grammatically frozen. This is
   grammatical adaptation, not translation of the name itself, but flagging the choice
   in case the maintainer prefers the undeclined form for strict identifier parity.

## Validation performed

Checked both slices against `cs.brief.part1.json`/`part2.json`: all 389 keys present
across the two output files with no key appearing in both, every `{0}`/`{1}`/`{2}`
placeholder set matches the English source per key, the literal tokens `{speech_lang}`
and `{text_lang}` in `Settings_Prompts_Help_BuiltInBody` are preserved verbatim and with
the same occurrence count as the English (`{speech_lang}` twice, `{text_lang}` once), and
no translated value over 25 characters is byte-identical to the English source.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Výchozí (doslovný přepis)"** — verbatim/word-for-word rendered as *doslovný (literal)*, paired with the already-established *přepis* for transcription; no new terms introduced.
