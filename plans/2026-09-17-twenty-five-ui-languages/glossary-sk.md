---
title: Slovak (sk) translation glossary
status: reference
created: 2026-09-18
---

# Slovak (sk) glossary

Companion to `sk.part1.json` (195 keys) and `sk.part2.json` (194 keys), 389/389 total.
Read `brief.md` first — this file only records the choices specific to Slovak.
Translated from the English source. A Czech translator worked from the same brief in
parallel, independently — no coordination happened, and Slovak terminology was chosen on
its own merits rather than by analogy to Czech (e.g. *súbor* not *soubor*, and several
whole-term choices below likely diverge further than spelling).

## Register

- **Buttons and short commands use the infinitive** — `Zrušiť`, `Zavrieť`, `Uložiť`,
  `Stiahnuť`, `Odstrániť`, `Otvoriť`, `Prehľadávať` — the standard Slovak (and Czech)
  software convention for controls. The infinitive is formality-neutral, so it sidesteps
  the `ty`/`vy` choice entirely for the tightest strings.
- **Body and descriptive text addressing the user directly uses the formal second-person
  plural** (`vy`, unstated) — `Vyberte…`, `Stlačte…`, `Skontrolujte…`, `Otvorte…` — matching
  traditional and current Microsoft Slovak localization for Windows consumer UI. Never the
  informal `ty` singular, and never mixed with the infinitive register within the same
  sentence type.
- Status labels (`Ready`, `Cancelled`) use neuter passive participles — `Pripravené`,
  `Zrušené` — the impersonal pattern Slovak status UI normally uses. A handful of
  "verb-in-progress" statuses (`Recording...`, `Loading model...`) instead use the
  reflexive present tense (`Nahráva sa...`, `Načítava sa model...`), which is the more
  natural Slovak rendering for an ongoing action and is not a break from the glossary noun
  `nahrávanie` — the noun is still used everywhere the *concept* of a recording is named.
- Sentence case throughout; no exclamation marks; no marketing language.

## Quotation convention

Slovak low–high double quotes: `„…“` (opening U+201E, closing U+201C) — e.g. `„Large v3
Turbo“`. Used for quoted model/prompt names and literal fragments. Never straight `"…"`.

ADR-032's slogan is kept in English inside Slovak quotes, as a citation of the ADR's own
title: `(ADR-032, „Local by default. Cloud by choice.“)`.

## Non-breaking space

Used between a number and a unit that stays untranslated per the never-translate register:
`Settings_SilenceTimeout_WaitFormat` → `"{0} ({1} s)"` (U+00A0 between `{1}` and `s`).
No other key needed one — sizes and version numbers arrive pre-formatted as opaque data
(`{1}` already contains `"1.5 GB"`), so spacing inside them isn't under my control.

## Core glossary

| Term | Slovak | Notes |
|---|---|---|
| dictation | diktovanie | verb: diktovať |
| transcribe / transcription | prepisovať / prepis | "recognized text" → "rozpoznaný text" |
| translate / translation | prekladať / preklad | never conflated with prepis |
| recording (live capture) | nahrávanie | live status uses the reflexive verb "Nahráva sa..." — see Register |
| speech engine | rozpoznávací motor | short form "motor" once page context is clear (`Settings_Engine_Title`); "motor" is the established Slovak IT sense (as in "vyhľadávací motor" for search engine), not the car-engine sense |
| model | model | unchanged, already a Slovak word |
| runtime | prostredie | "Prostredie Whisper", parallel to "Model Whisper" |
| source language | zdrojový jazyk | |
| target language | cieľový jazyk | |
| hotkey | klávesová skratka | matches Windows' own term |
| push-to-talk | podržanie | noun, used as mode badge and mid-sentence; gesture itself uses `Podržte {0}` (imperative "Hold") |
| toggle (mode) | prepínanie | noun, mode badge and mid-sentence |
| binding | priradenie / priradený (bound) | verb "byť priradené" for "is bound to" |
| tray | systémová lišta | matches common Slovak desktop-software usage for the notification area |
| widget | widget | kept as the established Slovak tech loanword, unrespelled |
| wait time / silence timeout | čas čakania | short nav title; body text spells out the full behaviour |
| punctuation | interpunkcia | |
| profanity filter | filter vulgarizmov | |
| inject / typed into | písať / napísať (do) | no invented technical term, plain "type into" phrasing throughout |
| clipboard | schránka | matches Windows' own term |
| cloud engine / cloud provider | cloudový motor / cloudový poskytovateľ | "poskytovateľ" is the standard Slovak IT term for provider |
| API key | kľúč API | |
| onboarding / tour | sprievodca | standard Slovak term for a guided walkthrough (as in "sprievodca inštaláciou") |
| restart required | vyžaduje sa reštart | |
| build (software, llama.cpp) | zostavenie | plural zostavenia |
| prompt (LLM instruction) | prompt / prompty | kept as the established loanword, matching Polish/Ukrainian precedent, rather than a native coinage that would collide with "hint"/"tooltip" vocabulary |
| backend | backend | kept — standard Slovak IT usage, matches Polish precedent |
| Cloud badge | Cloud | kept untranslated — Slovak IT usage for cloud computing overwhelmingly says "cloud", not the native "oblak" (which means a weather cloud), matching the `de`/`fr`/`it`/`nl` precedent noted in the skill |

## Case-inflection notes — keys where the raw `{0}` would force an oblique Slovak case

Slovak has six cases, same problem class as Polish and Ukrainian. Per the brief, every key
below substitutes a raw, uninflectable value (engine/runtime/model/language name) into a
slot the literal English phrasing would put in an oblique case or would force a
gender-agreeing predicate onto. Restructured so the slot sits after a colon, at a clause
boundary, or behind an invariant verb instead of an adjective — flagging each one as
instructed, since two or more independent languages converging on the same fix is meant to
signal a bug in the neutral file, not a per-language quirk:

- **`Transcribe_Status_RuntimeRestartRequiredFormat`** — "use the {0} runtime" would need
  the accusative of "prostredie" glued to an undeclined identifier in an awkward spot.
  Restructured with a colon: `"Reštartujte Parlotype, aby ste mohli použiť prostredie:
  {0}"`. (Matches the fix Polish and Ukrainian both made to this exact key.)
- **`Settings_Runtime_RestartNoteFormat`** — same problem, twice: `"Táto relácia už beží v
  prostredí: {0}. Whisper si prostredie vyberá len raz pri spustení, preto reštartujte
  Parlotype, aby ste prepli na prostredie: {1} — dovtedy sa nahrávanie nespustí."` Both
  runtime names now sit right after a colon, in label position.
- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — literal Slovak
  "Preložiť do {0}" needs the genitive of the language name, which the raw placeholder
  can't produce. Restructured to a label: `"Preklad: {0}"`. This is one of the three keys
  the localization skill calls out as flagged by every language that has translated this
  brief so far (`fr`, `uk`, `pl`, `fi`) — Slovak is a fifth data point for the same bug.
  `Language_ToggleSwitch_Translate` (the no-target fallback) was set to the same noun,
  `"Preklad"`, so the two keys read as one family.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Slovak
  idiomatically says "hovoríte po slovensky" (adverbial) or needs the instrumental for a
  direct object reading ("hovoríte slovenčinou"), neither of which the raw nominative
  placeholder can produce. Restructured around colons, reusing the structural fix (not the
  wording) every other language in this family found: `"Hovoríte: {0} → Parlotype píše:
  {1}."`
- **`Language_Toast_SourceUnsupportedFormat`** ("{0} isn't a source in {1}.") — two
  problems at once: the predicate "isn't a source" would need to agree in gender with
  whatever noun {0} turns out to be, which is unknowable from a raw value; and "in {1}"
  wants the locative case on the engine name. Fixed exactly like Polish's flagged version
  of this key, with an explicit classifying noun ("Jazyk") so the predicate agrees with
  *that* instead: `"Jazyk {0} nie je podporovaný ako zdrojový v motore: {1}. Použije sa
  rozloženie klávesnice."`
- **`Language_Toast_TargetUnsupportedFormat`** — same two problems, same fix: `"Jazyk {0}
  nie je dostupný v motore: {1}. Jazyk prekladu: {2}."`
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — "by {0}" would need the
  instrumental case on the engine name. Restructured to reuse the same "in motor: X"
  locative-plus-colon pattern as the two keys above, for one consistent solution across the
  file: `"Predchádzajúci cieľový jazyk bol resetovaný — nepodporovaný v motore: {0}."`
- **`Settings_Hotkeys_Conflict_ReservedFormat`** ("{0} is reserved: {1}") — literal "X is
  reserved" needs a predicate adjective ("vyhradené"/"vyhradený"/"vyhradená") agreeing with
  the grammatical gender of whatever the chord label {0} would be treated as, which isn't
  knowable. Restructured as two apposed labels instead of a subject–predicate sentence,
  using the gender-invariant neuter form as a generic tag rather than an agreeing
  predicate: `"{0} — vyhradené: {1}"`.
- **`Cloud_Error_ProviderUnavailableFormat`** ("{0} is unavailable right now...") — same
  predicate-gender-agreement risk as above, this time on a provider name
  (`Cloud_ProviderName_XaiGrok` is an indeclinable foreign name with no obvious Slovak
  gender). Fixed the way Ukrainian fixed the identical key: swap the adjective for an
  invariant present-tense verb that doesn't inflect for gender: `"{0} momentálne
  neodpovedá (HTTP {1}) — skúste to čoskoro znova. ({2})"`.

Keys I did **not** flag despite holding a name in a placeholder, and why:

- Anywhere the value is already quoted (`„{0}“`) — quoting neutralizes case in Slovak the
  same way it does in Polish and Ukrainian.
- Anywhere the value leads the sentence as the bare nominative subject of a present-tense
  verb (`Language_UnavailableNoteFormat`, `Language_Toast_EngineCannotTranslateFormat`,
  `Settings_Hotkeys_Conflict_ParameterHintsFormat`, `Settings_Hotkeys_Conflict_AltGrFormat`)
  — Slovak present-tense verbs don't inflect for gender, only past-tense ones do, so a
  nominative-subject-plus-present-tense-verb sentence never forces a case decision on the
  placeholder.
- **`Transcribe_Status_RuntimeUnavailableFormat`** ("{0} runtime not available") — resolved
  by leading with the noun instead: `"Prostredie {0} nie je k dispozícii..."` — {0} tags
  along as an appositive after the already-nominative noun, so nothing needs its own case.
- **`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat`** ("Left {0}"/"Right {0}") — Slovak
  "podržať" (hold) governs the accusative, and for a masculine-inanimate adjective like
  "ľavý"/"pravý" the accusative is identical to the nominative. So, unlike Polish and
  Ukrainian (which needed the genitive here and had to decline "ľavého"/"pravého"-style),
  Slovak's `"Ľavý {0}"`/`"Pravý {0}"` needed no special-cased form at all — a case where the
  same English key hits different languages differently.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to {1}.") — {0}
  is an already-localized gesture phrase, not a raw identifier, so it carries no case
  problem. {1} is the mode name, which the brief already splits into its own
  `Settings_Hotkeys_Conflict_Mode_*` keys specifically so each language can pick the form
  that fits — I added an explicit noun ("režimu") so {1} itself stays in its own nominative
  form (`podržanie`/`prepínanie`, identical to the standalone badges) rather than needing a
  declined variant: `"{0} je už priradené k režimu: {1}."`
- Version-number and size placeholders (`Settings_Updates_Status_AvailableFormat`,
  `Settings_Data_DeleteDialog_BodyFormat`, etc.) — digits and pre-formatted strings like
  `"1.5 GB"` don't decline in Slovak regardless of position.

## Windows-terminology matches

Used the actual Slovak Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Správca úloh**; its "Startup apps" tab → **Po spustení**; "Enable" →
  **Povoliť**.
- File Explorer → **Prieskumník**; Run dialog → **Spustiť**; Task View → **Zobrazenie
  úloh**; Xbox Game Bar → **Herný panel**; Windows Voice Typing → **Písanie hlasom
  Windows**.
- Lock workstation (Win+L) → **Uzamknúť počítač**.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — used **Ponuka rýchlych odkazov**
   as a literal, readable rendering. I could not verify a single fixed official Slovak
   Microsoft term for this menu from memory — flagged for a native check, same caveat the
   Ukrainian glossary recorded for its own equivalent.
2. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used **Premietanie** (a noun
   built from the verb Windows itself uses in this panel, "Premietať"). Worth checking
   against current Microsoft Slovak localization if exact parity matters — a shorter native
   label may exist that I don't have confident recall of.
3. **`Settings_Category_Audio`** — translated as **Zvuk**, matching Windows Sound settings
   terminology, over the loanword "Audio" which is also common in Slovak tech contexts.
   Category labels are short and this reads a little more native; either is defensible.
4. **Gender-neutral direct address** — body text uses the formal `vy`-register
   consistently and this register doesn't mark grammatical gender on the user the way
   Polish past-tense forms do, so no gendered-default judgment call was needed here (unlike
   Polish, which had to pick a default gender for past-tense verbs referring to the user).

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Predvolené (doslovný prepis)"** — verbatim/word-for-word rendered as *doslovný (literal)*, paired with the already-established *prepis* for transcription; no new terms introduced.
