---
title: Slovenian (sl) translation glossary
status: reference
created: 2026-09-18
---

# Slovenian (sl) glossary

Companion to `sl.part1.json` (195 keys) and `sl.part2.json` (194 keys), 389/389 total.
Read `brief.md` first — this file only records the choices specific to Slovenian.
Translated from the English source; `glossary-cs.md` and `glossary-sk.md` were read for
their *structural* solution to the placeholder/case problem only, not for word choice —
Slovenian terminology was chosen on its own merits (e.g. **mehanizem**, not
**modul**/**motor**, for "engine"; **sistemska vrstica**, not **oznamovací oblast**, for
tray). A Croatian translator worked from the same brief in parallel, independently — no
coordination happened and no term below was chosen by analogy to Croatian.

## Register

- **Formal address (vikanje), unstated "vi".** Body and description text addressing the
  user directly uses the formal second-person-plural imperative — "Izberite…",
  "Pritisnite…", "Odprite…" — never the informal "ti" (tikanje). This matches how Slovenian
  Windows and Slovenian commercial desktop software address the user, and the brief's
  instruction to pick one T/V register and never mix it.
- **Buttons and short commands use the short (2nd-singular-shaped) imperative** that
  Slovenian software conventionally uses for controls, independent of the vikanje/tikanje
  question — "Prekliči", "Shrani", "Izbriši", "Odpri", "Zapri", "Prenesi", "Uredi",
  "Podvoji", "Ponastavi", "Prebrskaj…". This is the actual convention real Slovenian
  Windows/Office buttons use (not the infinitive Czech and Slovak use for the same
  surface — Slovenian grammar treats this differently).
- **Status labels** ("Ready", "Cancelled", "Recording…") use neuter impersonal forms —
  "Pripravljeno", "Preklicano", "Snema se…" — the conventional Slovenian pattern for an
  abstract state that names no concrete noun. Where a badge sits directly beside a named,
  gendered noun instead (a model, a build, an installation, a prompt), the adjective agrees
  with that noun's real gender rather than defaulting to neuter — see "Badge gender
  agreement" below.
- Sentence case throughout; no exclamation marks; no marketing language.

## Quotation convention

Slovenian low–high double quotes: `„…“` (opening U+201E, closing U+201C) — e.g. `„Large v3
Turbo“`. Confirmed against the task brief and used for quoted model/prompt names and
literal fragments. Never straight `"…"`, never guillemets.

ADR-032's slogan is kept in English inside Slovenian quotes, as a citation of the ADR's own
title: `(ADR-032, „Local by default. Cloud by choice.“)`.

## Dash convention

Slovenian typography prefers the **en dash `–`** with a space on each side for the
parenthetical/list breaks the English source marks with an em dash `—` — e.g.
`Onboarding_Recording_EscLine`: "Esc – prekliče trenutno snemanje, ne da bi se karkoli
vtipkalo." `Settings_Hotkeys_LineFormat` (`"{0} — {1}"`) becomes `"{0} – {1}"` for the same
reason. Applied consistently across both slices.

## Non-breaking space

`Settings_SilenceTimeout_WaitFormat` → `"{0} ({1} s)"` with U+00A0 between `{1}` and
the unit `s`, so the number and its unit cannot wrap apart. No other key needed one — every
other pre-formatted size/version string (`"1.5 GB"`, `"0.4.4"`) arrives as opaque `{1}` data
whose internal spacing I don't control.

## Core glossary

| Term | Slovenian | Notes |
|---|---|---|
| dictation | **narekovanje** | verb: narekovati |
| transcribe / transcription | **prepisovati** / **prepis** | kept distinct from prevod throughout |
| translate / translation | **prevajati** / **prevod** | never used for transcribe |
| recording (live capture) | **snemanje** | live status uses the reflexive present "Snema se…" — see Register |
| speech engine | **(govorni) mehanizem** | short form "mehanizem" once page context is clear; matches the existing Slovenian tech sense in "iskalni mehanizem" (search engine), not the car-engine "motor" |
| model | **model** | unchanged, already a Slovenian word; masculine — see badge agreement below |
| runtime | **izvajalno okolje** | short: "okolje"; "Vulkan runtime" → "izvajalno okolje Vulkan" |
| backend (Whisper.net) | **zaledje** | neuter noun, folded together with "runtime" only where the English itself conflates them (`Settings_Runtime_Description`) |
| source language | **izvorni jezik** | |
| target language | **ciljni jezik** | |
| hotkey | **bližnjica** (tipkovna bližnjica) | matches Windows' own term; short "bližnjica" once context is clear |
| push-to-talk | **pridržanje** | noun, feminine? no — neuter-looking -je noun, actually neuter; badge and mid-sentence form; gesture uses "Pridržite {0}" (imperative "Hold") |
| toggle (mode) | **preklop** | noun, masculine; badge and mid-sentence form |
| binding (a configured hotkey) | **dodelitev / dodeljeno** ("bound to") | "{0} je že dodeljeno načinu: {1}." |
| tray | **sistemska vrstica** | chosen over the fuller official "področje za obvestila" for brevity in a terse UI — see "Things I was unsure about" |
| widget | **pripomoček** | matches the older Windows sense of a small always-on-top gadget |
| waveform | *(no key in this set surfaces this word alone)* | would be "valovna oblika" if needed later |
| wait time / silence timeout | **čas čakanja** | short nav title; body text spells out the full behaviour |
| punctuation | **ločila** | neuter plural, standard term for punctuation marks |
| profanity filter | **filter kletvic** | "kletvica" = swear word |
| inject / typed into | **vtipkati / se vtipka (v)** | plain "typed into" phrasing throughout, never "vstaviti" (insert/paste), to avoid confusion with the clipboard |
| clipboard | **odložišče** | standard Slovenian Windows term |
| cloud engine / cloud provider | **oblačni mehanizem** / **oblačni ponudnik** | |
| Cloud (badge) | **Oblak** | translated, not kept as the loanword — Slovenian "oblak" is the natural, well-understood word for cloud computing (unlike `de`/`fr`/`it`/`nl`, which keep "Cloud"), matching the `pl`/`uk`/`pt` precedent |
| API key | **ključ API** | matches cs/sk's genitive-style compound over a calque "API ključ" |
| onboarding / tour | **vodnik** | "Parlotype tour" → "Vodnik po Parlotype" |
| restart required | **potreben je ponovni zagon** (heading) / **znova zaženite …** (body) | |
| build (software, llama.cpp) | **gradnja** | feminine noun; plural "gradnje" |
| prompt (LLM instruction) | **poziv** / **pozivi** | chosen as the native Slovenian AI-tooling term (used this way in Slovenian ChatGPT-class UI) over the loanword "prompt" that cs/sk kept |
| placeholder (`{speech_lang}`/`{text_lang}` tokens) | **spremenljivka** / **spremenljivke** | distinct from the numbered-`{0}` sense, which is never named as a concept in user-facing copy |
| Cloud badge prefix ("Cloud: OpenAI-compatible") | **Oblak: …** | kept consistent with the Transcribe_CloudBadge translation |

## Badge gender agreement

Several one-word badges are adjectives/participles that sit beside a specific, known-gender
noun rather than functioning as an abstract status. I matched the adjective's gender to that
noun instead of defaulting to neuter, since the referent's gender is fixed and known (unlike
a name arriving in a raw placeholder):

- `Settings_Model_Installed` ("Installed", shared by the Gemma 4 and Parakeet model lists) →
  **Nameščen**, masculine, agreeing with **model**.
- `Settings_WhisperModel_NotDownloaded` ("(not downloaded)", sits beside a model name) →
  **(ni prenesen)**, masculine, same reason.
- `Settings_Prompts_BuiltInBadge` ("Built-in") → **Vgrajen**, masculine, agreeing with
  **poziv**.
- `Settings_LlamaCpp_ManagedBadge` / `ManualBadge` ("Managed"/"Manual", on an install row) →
  **Upravljana** / **Ročna**, feminine, agreeing with **namestitev** (installation).
- `Settings_LlamaCpp_NotManagedBadge` ("Not managed by Parlotype") → **Ni upravljana s
  strani Parlotype**, same feminine agreement.
- `Settings_LlamaCpp_InstalledBadge` ("Installed", per-row badge in the Available builds
  list) → **Nameščena**, feminine, agreeing with **gradnja** (build) — distinct from
  `Settings_Model_Installed` above, which is masculine because it labels a different noun.
- `Settings_LlamaCpp_AudioSupported` / `AudioNotSupported` (value shown after the "Zvok:"
  label) → **Podprt** / **Ni podprt**, masculine, agreeing with **zvok** (audio).
- `Settings_CloudProviders_KeySavedBadge` ("✓ Saved") → **✓ Shranjen**, masculine, agreeing
  with **ključ**, matching `Settings_CloudProviders_KeyStatus_Saved`'s "Ključ shranjen".

By contrast, abstract connection/process status words with no single concrete referent
(`Transcribe_Status_Ready`, `_Cancelled`, `Settings_LlamaCpp_Status_Connected`,
`_Disconnected`, `_NotProbed`) stay **neuter impersonal** ("Pripravljeno", "Preklicano",
"Povezano", "Ni povezano", "Ni preverjeno") — the conventional Slovenian UI pattern for a
state description that isn't grammatically modifying one particular noun.

## The dual

Slovenian marks exactly two of something distinctly from one and from many, and I used it
in four places where the English source genuinely counts a pair:

1. **`Onboarding_Cloud_Body`** — "The two cloud engines are off by default and require your
   own API key." The English explicitly says "the two," so I used the dual throughout the
   sentence: **"Oba oblačna mehanizma sta privzeto izklopljena in zahtevata lasten ključ
   API."** (`oba` dual "both," `oblačna` dual adjective, `mehanizma` dual noun, `sta`/
   `zahtevata` dual verb forms.) `Onboarding_Cloud_Title` stays plain plural
   ("Oblačni mehanizmi so izbirni") since the title itself never says "the two."
2. **`Settings_Hotkeys_ModeToggleTooltip`** — "Switch between push-to-talk and toggle."
   Exactly two named modes, so I used the dual instrumental of *način* (mode):
   **"Preklopi med načinoma pridržanja in preklopa."**
3. **`Settings_Language_Description`** — "…The connector between them turns translation on
   and off." "Them" is exactly the source and target language cards described in the
   previous sentence — a genuine pair. I rendered "them" as the dual pronoun **njima**:
   **"Povezovalni element med njima vklopi in izklopi prevajanje."**
4. **`Cloud_BaseUrl_PlainHttp`** — "…the API key and audio would leave this machine
   unencrypted." Two coordinated masculine singular subjects (**ključ** and **zvok**) joined
   by "in" trigger dual verb agreement in Slovenian: **"…ključ API in zvok bi ta računalnik
   zapustila nešifrirano."** (`zapustila`, dual conditional, not the plural `zapustili`.)

I did **not** use the dual for `Language_Summary_Format` ("You speak {0} → Parlotype types
{1}.") — the two clauses have different singular subjects ("you", "Parlotype"), not a single
dual subject, so ordinary singular verb forms are correct there.

## Case-inflection notes — keys where the raw `{0}` would force an oblique Slovenian case

Slovenian has six cases, the same problem class as Czech, Slovak, Polish and Ukrainian.
Every key below substitutes a raw, uninflectable value (an engine, runtime or language name)
into a slot the literal English phrasing would put in an oblique case or would force a
gender-agreeing predicate onto. Restructured so the slot sits after a colon, at a clause
boundary, or behind an invariant verb instead of an adjective — flagging each one as
instructed, since independent languages converging on the same fix signals a bug in the
neutral file, not a per-language quirk:

- **`Transcribe_Status_RuntimeRestartRequiredFormat`** — "use the {0} runtime" would need
  the accusative of "izvajalno okolje" glued to an undeclined identifier. Restructured with
  a colon: `"Za uporabo izvajalnega okolja: {0} znova zaženite Parlotype"`. (Same fix Czech
  and Slovak both made to this exact key.)
- **`Settings_Runtime_RestartNoteFormat`** — same problem, twice: `"Ta seja že uporablja
  izvajalno okolje: {0}. Whisper izvajalno okolje izbere samo ob zagonu, zato za preklop na
  izvajalno okolje: {1} znova zaženite Parlotype – do takrat se snemanje ne bo začelo."` Both
  runtime names now sit right after a colon, in label position.
- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — literal Slovenian
  "Prevedi v {0}" needs the accusative/locative of the language name after "v," which the
  raw placeholder can't produce. Restructured to a label: `"Prevod: {0}"`. This is one of the
  keys the localization skill calls out as flagged by every language that has translated
  this brief so far (`fr`, `uk`, `pl`, `fi`, `cs`, `sk`) — Slovenian is another data point for
  the same bug. `Language_ToggleSwitch_Translate` (the no-target fallback) was set to the
  same noun, `"Prevod"`, so the two keys read as one family.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Slovenian
  idiomatically wants an adverb for "you speak Spanish" ("govorite špansko") or the
  accusative for a direct-object reading, neither of which a raw, un-inflected language name
  can supply. Restructured around colons, matching the structural fix (not the wording)
  every other language in this family found: `"Govorite: {0} → Parlotype tipka: {1}."`
- **`Language_Toast_SourceUnsupportedFormat`** ("{0} isn't a source in {1}.") — two problems
  at once: the predicate would need to agree in gender with whatever noun {0} turns out to
  be, unknowable from a raw value; and "in {1}" wants the locative case on the engine name.
  Fixed with an explicit classifying noun ("Jezik") so the predicate agrees with *that*
  instead, plus a colon before the engine name: `"Jezik {0} ni podprt kot izvorni v
  mehanizmu: {1}. Uporabljena bo postavitev tipkovnice."`
- **`Language_Toast_TargetUnsupportedFormat`** — same two problems, same fix: `"Jezik {0} ni
  na voljo v mehanizmu: {1}. Jezik prevoda: {2}."`
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — "by {0}" would need the
  instrumental/genitive on the engine name in a passive construction. Flipped to active
  voice so {0} becomes the nominative subject instead: `"Prejšnji ciljni jezik je bil
  ponastavljen – mehanizem {0} ga ne podpira."` A bare "mehanizem" + name apposition in the
  nominative needs no case ending regardless of what the name turns out to be.
- **`Settings_Hotkeys_Conflict_ReservedFormat`** ("{0} is reserved: {1}") — literal "X is
  reserved" needs a predicate adjective ("rezerviran/-a/-o") agreeing with the grammatical
  gender of whatever the chord label {0} would be treated as, which isn't knowable.
  Defaulted to the neuter form as a generic tag for a symbolic chord string (not a real
  noun): `"{0} je rezervirano: {1}"`.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to {1}.") — {1}
  is the mode name (`Settings_Hotkeys_Conflict_Mode_*`), which the brief already splits into
  its own keys specifically so each language can pick the form that fits. I added an
  explicit noun ("načinu") so {1} stays in its own nominative form (`pridržanje`/`preklop`,
  identical to the standalone badges) rather than needing a declined variant:
  `"{0} je že dodeljeno načinu: {1}."`
- **`Cloud_Error_ProviderUnavailableFormat`** ("{0} is unavailable right now…") — the only
  `Cloud_Error_*`/`Cloud_NotConfigured_*` key that does **not** already put {0} ahead of a
  colon (see `Cloud_ProviderName_*`'s own comment, which asks for exactly that pattern). A
  literal adjective ("nedosegljiv/-a/-o") would force gender agreement Slovenian cannot
  resolve for an indeclinable foreign provider name. Rewrote with an invariant present-tense
  verb instead of an adjective: `"{0} trenutno ne deluje (HTTP {1}) – poskusite znova čez
  nekaj časa. ({2})"` — "ne deluje" carries no gender in the present tense, so it is safe
  regardless of the provider name's grammatical gender.

Keys I did **not** flag despite carrying a name in a placeholder: anywhere the value is
already quoted (`„{0}“` — a quoted citation is conventionally left undeclined in Slovenian
UI copy, the same trick cs/sk/pl/uk use), anywhere the value leads the sentence as the bare
nominative subject of a present-tense verb (`Language_UnavailableNoteFormat`,
`Language_Toast_EngineCannotTranslateFormat`, `Settings_Hotkeys_Conflict_ParameterHintsFormat`,
`Settings_Hotkeys_Conflict_AltGrFormat` — Slovenian present-tense verbs don't inflect for
gender, only past-tense/participle forms do), and:

- **`Transcribe_Status_RuntimeUnavailableFormat`** ("{0} runtime not available") — resolved
  by leading with the noun instead: `"Izvajalno okolje {0} ni na voljo…"` — {0} tags along
  as a nominative appositive after the already-nominative noun, so nothing needs its own
  case.
- **`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat`** ("Left {0}"/"Right {0}") —
  Slovenian "pridržati" (hold) governs the accusative, and for a masculine-inanimate
  adjective like "levi"/"desni" the accusative is identical to the nominative. So, like
  Slovak (and unlike Polish/Ukrainian, which needed the genitive here), Slovenian's `"Levi
  {0}"`/`"Desni {0}"` needed no special-cased form — "Pridržite Levi Ctrl" declines nothing.
- Version-number and size placeholders (`Settings_Updates_Status_AvailableFormat`,
  `Settings_Data_DeleteDialog_BodyFormat`, etc.) — digits and pre-formatted strings like
  `"1.5 GB"` don't decline in Slovenian regardless of position.

## Windows-terminology matches

Used the actual Slovenian Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Upravitelj opravil**; its "Startup apps" tab → **Zagon**; "Enable" →
  **Omogoči** (`Settings_Startup_BlockedBody`, `Settings_Startup_WhatChangesBody`,
  `Settings_Startup_State_BlockedByWindows`).
- File Explorer → **Raziskovalec**; the Run dialog → **Zaženi**; Task View → **Pregled
  opravil**; Lock workstation (Win+L) → **Zakleni računalnik**; Personalization's Theme
  page → **Tema** (`Settings_Theme_Title`).
- `Settings_Category_Audio` → **Zvok**, matching the Windows Sound settings page name.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — used **"Meni s hitrimi
   povezavami"** as a literal, readable rendering. I could not verify a single fixed
   official Slovenian Microsoft term for this menu from memory — flagged for a native
   check, the same caveat cs/sk recorded for their own equivalents.
2. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used **"Projeciranje zaslona"**,
   built from the verb Windows itself uses in this panel ("Projeciraj"). Worth checking
   against current Microsoft Slovenian localization if exact parity matters.
3. **Tray = "sistemska vrstica"** rather than the fuller official Microsoft term
   "področje za obvestila" (notification area). I chose the shorter, colloquially
   well-understood form because the brief stresses terseness and this term recurs often
   (tour, tray body text, startup description); the official phrase would run noticeably
   longer across several strings. Worth a native check if strict Microsoft-glossary parity
   matters more than brevity here.
4. **`Settings_Category_Input`** ("Input") — translated as **Vnos**. Generic enough to
   match the English's own vagueness (it groups the Hotkeys page), but exactly as
   underspecified in Slovenian as it is in English — flagging it as the cs/sk glossaries
   do, for the same reason.
5. **"poziv/pozivi" vs the loanword "prompt"** — chose the native term to match how
   Slovenian AI-tooling UIs (e.g. localized chat assistants) already render this concept,
   diverging deliberately from cs/sk's choice to keep the English loanword. A native reader
   used to Slovenian AI product UI will recognize "poziv" immediately; flagging the
   divergence in case the maintainer wants one term across all Slavic languages.
6. **`Settings_Hotkeys_Conflict_ParameterHintsFormat`** — declined "Visual Studio" into the
   locative ("v Visual Studiu"), common in Slovenian technical writing, while leaving "VS
   Code" undeclined right after it in the same clause ("v Visual Studiu in VS Code"). This
   is grammatical adaptation of the first name only, not translation of either identifier;
   flagging the asymmetry in case the maintainer prefers both left undeclined.
7. **`Settings_Hotkeys_Reserved_GameBar`** — rendered as **"Odpri vrstico za igre"** (open
   the game bar) rather than keeping "Xbox Game Bar" as a product name, since the feature
   this shortcut actually opens is Windows' own "Vrstica za igre" panel, not a separately
   branded product in Slovenian Windows UI. Worth a check against current wording.

## Validation performed

Checked both slices against `sl.brief.part1.json`/`part2.json` programmatically: all 389
keys present across the two output files with no key appearing in both, every `{0}`/`{1}`/
`{2}` placeholder set matches the English source per key, the literal tokens `{speech_lang}`
and `{text_lang}` in `Settings_Prompts_Help_BuiltInBody` are preserved verbatim and with the
same occurrence count as the English (`{speech_lang}` twice, `{text_lang}` once), and no
translated value over 25 characters is byte-identical to the English source.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Privzeto (dobesedni prepis)"** — verbatim/word-for-word rendered as *dobesedni (literal)*, paired with the already-established *prepis* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Pridržite desni Ctrl`.**

Side is now `desni {0}` / `levi {0}`, lowercase. `Conflict_AlreadyBoundFormat` quotes the imperative gesture: `„{0}“ je že dodeljeno načinu: {1}.`
