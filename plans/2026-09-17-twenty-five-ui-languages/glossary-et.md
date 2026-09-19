---
title: Estonian (et) translation glossary
status: reference
created: 2026-09-18
---

# Estonian (et) glossary

Companion to the 389-key Estonian translation (delivered as three slices,
`et.part1.json`–`et.part3.json`, merged by the importer). Read `brief.md` first — this
file only records the choices specific to Estonian.

## Why Estonian needed the Finnish technique (with a variant)

Estonian marks fourteen cases with suffixes, exactly the problem Finnish solved by
compounding: a `{0}` substituted at runtime arrives bare and cannot take an ending, so a
sentence that would naturally put a case suffix directly on the runtime/model/language
name is impossible. Estonian compounds just as freely as Finnish, so the same fix works:
attach the raw placeholder as an **invariant hyphen-prefix to a native noun**, and let only
that native noun carry the case:

- `"the {0} runtime"` → `"{0}-käitusaeg"` (nominative), `"{0}-käitusaega"` (partitive,
  used as the object of *kasutama*/*lülituma*), `"{0}-käitusajale"` (allative, "switch to
  X") — `{0}` never changes shape, only `käitusaeg` (the Estonian term Microsoft itself
  uses for "runtime", e.g. ".NET-i käitusaeg") inflects.
- an engine name used with "in X" → `"{1}-mootoris"` (inessive on `mootor`, "engine", not
  on the engine-name placeholder).

Where the placeholder itself is the head noun with nothing to hang a suffix on — chiefly a
bare **language name** used as a grammatical object, or an external string arriving as a
passive agent — compounding doesn't apply, and I restructured with a colon or flipped
passive-to-active instead, the same technique `fr`/`uk`/`pl`/`fi` converged on
independently. Both techniques, and every key that needed either, are listed below.

I did **not** hyphenate the language-name cases the way I hyphenated runtime/engine
names — see the case-inflection list below for why: a bare language name has no natural
Estonian "head noun" (unlike "runtime"/"engine") to hyphenate onto, so those went to
colon-restructuring instead, same as the non-compounding languages.

## Register

**Second-person singular, pronoun dropped where the verb ending already carries it**
("Räägid soome keelt", not "Sina räägid...") for descriptive/body copy, and the **plain
imperative** for buttons and instructions ("Salvesta", "Tühista", "Ava kaust") — this
matches Estonian Windows/Office localization (`Salvesta`, `Tühista`, `Ava`, `Kustuta`) and
mirrors how Finnish was handled for the same reason: Estonian has no `sina`/`teie`
politeness split in consumer software the way German or Russian does, so no second choice
was needed. Sentence case throughout, no exclamation marks, no marketing language.

## Quotation convention

Estonian typographic quotation marks: **opening „ (U+201E), closing " (U+201C)** — e.g.
`„Automaatne tuvastus"`. Checked and used consistently for quoted UI option names, model
names embedded in sentences, and the ADR-032 slogan, kept in English inside the quotes as
a citation exactly as the other language files do:
`(ADR-032, „Local by default. Cloud by choice.")`.

## Letters

`õ ä ö ü š ž` — checked. `õ` appears distinct from `ö` throughout (`või`, `sõltub`,
`täieõiguslik`, `sõna` — never collapsed to `vöi`/`söna`). `š`/`ž` did not arise naturally
in this key set (no borrowed words needed them); flagging that absence rather than
silently skipping the check.

## Core glossary

| Term | Estonian | Notes |
|---|---|---|
| dictation | **dikteerimine** (verb: dikteerima) | App's core noun/verb throughout. |
| transcribe / transcription | **transkribeerima** / **transkriptsioon** | Established Estonian term for turning speech into text; kept strictly distinct from tõlkima. |
| translate / translation | **tõlkima** / **tõlge** (the noun), **tõlkimine** (the act) | Never used for transcribe. |
| recording (live capture) | **salvestus** (noun), status line uses the verb **salvestamine** | "Recording..." → "Salvestamine...". |
| speech engine | **kõnetuvastusmootor**, short nav form **mootor** | `Settings_Category_SpeechEngine` (category header, less room) uses the shorter **Kõnetuvastus**; `Settings_Engine_Heading` (page heading, more room) uses the full **Kõnetuvastusmootor**; the settings-nav label `Settings_Engine_Title` uses the short **Mootor**. |
| model | **mudel** | Already a native Estonian word. |
| runtime | **käitusaeg** | The term Microsoft's own Estonian localization uses for ".NET Runtime". Compounds with an untranslated identifier: `Vulkan-käitusaeg`. |
| source language | **lähtekeel** | Standard Estonian translation-studies term. |
| target language | **sihtkeel** | Standard Estonian translation-studies term. |
| hotkey | **kiirklahv** | Standard Estonian Windows term for a keyboard shortcut. |
| push-to-talk | **klahvihoidmine** (noun, "key-holding") | Badge: `Klahvihoidmine`. The gesture description itself uses the plain verb phrase "hoia all" (hold down), see case-inflection notes. |
| toggle | **lülitus** | Badge: `Lülitus`. |
| binding | **seos** | Not separately surfaced as standalone UI copy in this key set; recorded for glossary completeness. |
| tray | **teavitusala** | Official Windows Estonian term for the notification area. |
| widget | **vidin** | Matches Windows 11's own Estonian term for "widgets" (`Vidinad`). |
| waveform | **lainevorm** | Not surfaced as standalone copy in this key set; recorded for completeness. |
| wait time / silence timeout | **ooteaeg** (concept), title **Vaikuse ajalõpp** | The settings title spells out "silence timeout" literally as a fixed heading; body/option text uses the shorter "oota" verb construction or the plain duration names (Medium/Long/...). |
| punctuation | **kirjavahemärgid** | |
| profanity filter | **roppuste filter** | |
| inject / typed into | **kirjutama** ("kirjutatakse") | One verb for the whole concept; no invented technical term. |
| clipboard | **lõikelaud** | Standard Windows Estonian term. |
| cloud engine / cloud provider | **pilvemootor** / **pilveteenuse pakkuja** | Short badge: `Pilv`. |
| API key | **API-võti** | |
| onboarding | **tutvustus** | "Parlotype tour" → `Parlotype'i tutvustus`; "Skip tour" → `Jäta tutvustus vahele`. |
| restart required | **Nõuab taaskäivitamist** | |
| build (software) | **versioon** | Kept consistent with how "version" is already handled elsewhere (`Settings_Updates_*`); avoids introducing a second word for the same thing users see as "Build:" / "development build", matching the Finnish precedent for this exact call. |
| prompt (LLM instruction) | **viip** (plural viibad) | Extends the already-established Estonian term for a command-line prompt (`käsurea viip`) to the LLM-instruction sense, the same kind of minimal-risk extension Finnish made with `kehote`. |

## Case-inflection notes — resolved by compounding (no flag needed)

The raw placeholder became an invariant hyphen-prefix; only the trailing Estonian noun
carries the case:

- **Transcribe_Status_RuntimeRestartRequiredFormat** → `"...et kasutada
  {0}-käitusaega"` (partitive on `käitusaeg`, `{0}` untouched).
- **Transcribe_Status_RuntimeUnavailableFormat** → `"{0}-käitusaeg ei ole
  saadaval..."` (nominative subject — no case needed at all here).
- **Settings_Runtime_RestartNoteFormat** → `"...kasutab juba {0}-käitusaega...
  lülituda üle {1}-käitusajale..."` (partitive, then allative, both on
  `käitusaeg`).
- **Language_Toast_SourceUnsupportedFormat** and the engine half of
  **Language_Toast_TargetUnsupportedFormat** → `"{1}-mootoris"` (inessive on
  `mootor`, not on the engine-name placeholder).

## Case-inflection notes — keys where a raw `{0}` would need an Estonian case suffix

Per the brief, every key where the literal English phrasing would force Estonian to
inflect a placeholder it cannot touch, and how each was restructured:

- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — `{0}` is a
  language name; "to X" wants the illative/translative case on the name itself
  (`"...kseks"`/`"...keelde"`), impossible on a raw substituted value. Restructured to
  **`"Tõlgi: {0}"`** (label + colon), matching `Language_ToggleSwitch_Translate`'s
  fallback ("Tõlgi").
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Estonian
  "rääkima" wants a partitive object ("räägid soome keelt"), and "kirjutama" the same,
  neither of which a raw nominative placeholder can produce cleanly for an arbitrary
  language name. Restructured to **`"Räägid: {0} → Parlotype kirjutab: {1}."`**, reusing
  the arrow the English already has and adding a colon after each verb — the same fix
  Finnish, French, Polish and Ukrainian converged on independently.
- **`Language_Toast_TargetUnsupportedFormat`** ("...Translation set to {2}.") — "set
  to X" wants the translative case (`"-ks"`) directly on the language name. The
  engine-name half of this key (`{1}`) was solved by compounding (see above); this
  clause was restructured with a label instead: **`"...Tõlke sihtkeel: {2}."`**
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — a literal
  translation wants the raw engine name as a passive agent, which Estonian marks with a
  postposition phrase that still needs the noun in genitive (`"{0} poolt"` reads as
  translated-from-English and still forces a case-like construction). Rather than force
  anything onto an unknown value, the sentence was flipped from passive-with-agent to
  active voice, exactly as Finnish did: **`"Eelmine sihtkeel lähtestati — {0} ei toeta
  seda."`** — `{0}` is now a bare nominative subject, no case needed regardless of what
  value arrives.
- **`Settings_Hotkeys_Gesture_HoldFormat`** ("Hold {0}") and
  **`Settings_Hotkeys_Gesture_DoubleTapFormat`** ("Double-tap {0}") — `{0}` here is an
  already-localized key-name phrase (e.g. "Parem Ctrl"). A natural Estonian imperative
  ("Hoia {0} all") would make that phrase the direct object of a verb wanting partitive
  case, which would in turn require inflecting the *adjective* inside it
  (`Vasak`/`Parem`) — but those words are shared with `Settings_Hotkeys_Modifier_LeftFormat`
  /`RightFormat`, which must stay nominative everywhere else they're used. Restructured
  both as label-plus-colon: **`"Hoia all: {0}"`** / **`"Topeltvajutus: {0}"`**. This
  composes into `Hotkey_Hint_TalkFormat`/`DictateFormat` less smoothly than a plain
  phrase would (see "Things I was unsure about" below), but keeps every reused
  sub-phrase in a single, always-valid nominative form.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to {1}.")
  — `{1}` here is `Settings_Hotkeys_Conflict_Mode_PushToTalk`/`Toggle`, which Estonian
  "seotud" (bound) would otherwise want in the comitative case (`"seotud
  millega?"`). Restructured with a colon instead of a case-bearing preposition:
  **`"{0} on juba seotud: {1}."`**

No other key needed this treatment. Two things worth recording for future reference,
matching what Finnish found: (1) Estonian predicate adjectives/participles
(`saadaval`, `reserveeritud`) don't agree in gender with their subject — Estonian has no
grammatical gender — so `Cloud_Error_ProviderUnavailableFormat` needed no rework despite
a subject of unknown "gender"; (2) numeric placeholders (version numbers, HTTP codes,
sizes) never visibly inflect in digit form and readily take a hyphenated case suffix
directly on the digits themselves when needed (`Onboarding_Progress_Format` →
**`"{0}. samm {1}-st"`**, elative `-st` "out of" attached straight to the raw number,
which is standard Estonian orthography), so none of the `Settings_Updates_Status_*` or
`Cloud_Error_*` keys needed anything beyond plain substitution.

## Length

Estonian ran close to the English throughout — noticeably shorter than Finnish would,
since Estonian has fewer bound suffixes stacking on any one word — and nothing required
abandoning a literal rendering for a shorter paraphrase except:

- **`Transcribe_Source_Auto`** ("Auto", narrow language chip) — kept as **`"Auto."`**
  (with a period, short for "automaatne") rather than the full **`"Automaatne"`** used in
  `Settings_Runtime_Auto_Name`'s longer context, purely to keep the narrow chip's width;
  unlike Finnish, bare "Auto" in Estonian carries no confusing alternate meaning, so no
  ambiguity problem, just a width one.
- **`Settings_SilenceTimeout_Title`** ("Silence timeout") → **`"Vaikuse ajalõpp"`** —
  runs a little longer than the English but is the natural, idiomatic Estonian pairing;
  no shorter alternative reads as clearly as a settings heading.

Everything else — onboarding paragraphs, settings descriptions — landed within about
10–20% of the English length, well inside the space the `_Body` keys have.

## Windows-terminology matches

Used real Estonian Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Tegumihaldur**; its Startup tab → **Käivitamine**; "Enable" →
  **Luba**.
- File Explorer → **Failihaldur**; the Run dialog → **Käivita**; Task View →
  **Tegumivaade**; the Ctrl+Alt+Del screen → **Turvaekraan**; Windows Voice Typing →
  **Windowsi häälsisestus**; the Settings app → **Seaded**.
- The lock-workstation shortcut is rendered as **Lukusta arvuti**, matching the verb
  Windows itself uses for locking (**Lukusta**) rather than a literal "lock workstation".

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — could not verify a single
   fixed official Estonian name for this menu from memory. Used **`"Kiirvalikute menüü
   (Win+X)"`** as a readable, disambiguated rendering; flagging for a native check, same
   caveat Finnish and Ukrainian raised for the same key.
2. **`Settings_Hotkeys_Reserved_GameBar`** — kept **`"Ava Xbox Game Bar"`**, leaving the
   brand name untranslated; worth checking against current Microsoft Estonian
   localization for whether it uses a partial translation instead.
3. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used **`"Ekraani
   kuvamine"`**; Windows' own Estonian wording for this specific pane may differ —
   flagged for a native check, matching Finnish's flag on the same key.
4. **`Settings_Category_Input`** ("Input") — translated literally as **`"Sisend"`**,
   the same judgment call Polish and Finnish flagged for the same key: generic enough to
   match the English's own vagueness, but worth a second look once the category's exact
   contents are final.
5. **`ModelDownload_ConfirmWithProjectorFormat` / `Settings_Gemma4Model_Description`**
   — "vision projector" (the `mmproj` component) was rendered as **`"nägemisprojektor"`**,
   a coinage rather than an established Estonian term (none is known to me). Flagging in
   case a more standard Estonian AI/ML term already exists, same caveat Finnish raised
   for its own coinage.
6. **`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`** composing into
   **`Hotkey_Hint_TalkFormat`/`DictateFormat`/`WithCancelFormat`** — see the
   case-inflection section above. Rather than grafting an Estonian purpose clause onto
   the end of an already colon-terminated gesture label, these reuse the **`·`**
   middle-dot separator the English source already uses in `WithCancelFormat`, for all
   three: `"{0} · räägi"`, `"{0} · dikteeri"`, `"{0} · Esc katkestamiseks"`. The composed
   result (e.g. `"Hoia all: Parem Ctrl · räägi"`) is grammatically valid and consistent
   but reads more like a labeled fact than one flowing instruction — the same trade-off
   Finnish recorded for the identical composition problem. If the maintainer ever
   revisits key composition, a single combined key per hint (rather than composing two
   independently-formatted pieces) would let Estonian phrase this more naturally.
7. **`viip`/`viibad` for "prompt"** — this extends an established sense (a command-line
   prompt) to a new one (an LLM instruction) rather than reusing a term with prior
   attestation in that exact sense. I'm fairly confident in it (short, already inflects
   cleanly, no clash with any other glossary term) but it is a judgment call worth a
   second look from a native speaker working in Estonian AI tooling.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Vaikimisi (sõnasõnaline transkriptsioon)"** — verbatim/word-for-word rendered as *sõnasõnaline (word-for-word)*, paired with the already-established *transkriptsioon* for transcription; no new terms introduced.
