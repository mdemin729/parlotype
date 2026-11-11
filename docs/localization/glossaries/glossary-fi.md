---
title: Finnish (fi) translation glossary
status: reference
created: 2026-09-17
---

# Finnish (fi) glossary

Companion to the 389-key Finnish translation (delivered as four slices,
`fi.part1.json`–`fi.part4.json`, merged by the importer). Read `brief.md` first — this
file only records the choices specific to Finnish.

## Why Finnish needed a different strategy than Polish/Ukrainian/Russian

Those languages solved the placeholder-case problem by moving the slot after a colon or
to a clause edge, because a raw `{0}` cannot carry the case ending their grammar demands.
Finnish has the same problem (fifteen cases) but also a tool the Slavic languages don't:
**compounding**. In Finnish only the *last* element of a compound noun inflects; an
attached first element is invariant. So instead of forcing `{0}` itself to inflect, most
runtime/model/engine names were turned into the **first half of a hyphenated compound**,
with a native Finnish noun carrying the case ending:

- `"the {0} runtime"` → `"{0}-ajoympäristö"` (nominative), `"{0}-ajoympäristöä"`
  (partitive), `"{0}-ajoympäristöön"` (illative) — `{0}` never changes shape, only
  `ajoympäristö` conjugates.
- `"in {1}"` (an engine) → `"{1}-moottorissa"` (inessive on `moottori`, not on `{1}`).

This resolved several keys Polish/Ukrainian had to flag as unsolvable. Where the
placeholder itself is the head noun with nothing to compound onto — chiefly bare
**language names** used as a grammatical object or with a preposition — compounding
doesn't apply, and those keys were restructured with a colon instead, the same technique
the other languages used. Both techniques, and every key where compounding wasn't enough,
are listed below.

## Register

**Second-person singular, pronoun dropped where the verb ending already carries it**
("Puhut", not "Sinä puhut"; "Valitse teema", not an explicit imperative-with-pronoun
form) — this matches how Finnish Windows and Office are localized: instructions read as
plain imperatives (`Tallenna`, `Peruuta`, `Avaa kansio`) and descriptive body copy uses
the second-person-singular verb form or a passive when the subject is generic ("Tämä
poistaa...", "Muutokset tulevat voimaan..."). No `sinä`/`te` distinction was needed —
Finnish doesn't have a T/V split the way German or Russian does; the closest analogue
(the `te`-form of politeness) is essentially absent from consumer software and was not
used anywhere.

Buttons and short controls use the plain imperative (`Tallenna`, `Peruuta`, `Avaa`,
`Poista`), matching Windows' own button style. Sentence case throughout, no exclamation
marks, no marketing language.

## Quotation convention

Finnish typographic quotation marks: **the same character, U+201D (”), on both sides** —
e.g. `”Automaattinen tunnistus”`. This is standard Finnish (and Swedish) typography and
differs from English's opening/closing pair. Used for quoted UI option names, prompt
placeholder syntax examples, and the ADR-032 slogan, which is kept in English inside the
quotes as a citation, exactly as the Russian and Polish files do:
`(ADR-032, ”Local by default. Cloud by choice.”)`.

## Non-breaking space

Used between a number and a unit that stays untranslated per the never-translate
register: `Settings_SilenceTimeout_WaitFormat` → `"{0} ({1} s)"`, and between the
number and the spelled-out unit in `Settings_Updates_CadenceNote` (`"6 tunnin
välein"`) to stop an orphaned numeral at a line wrap in the narrow widget. `GB`/`MB`
inside fixed descriptive sentences (`"~670 MB"`, `"~10 GB"`) keep the English abbreviation
verbatim per the never-translate register — not localized to Finnish `Gt`/`Mt` — with a
plain space, matching how the English source already formats them.

## Core glossary

| Term | Finnish | Notes |
|---|---|---|
| dictation | **sanelu** (verb: sanella) | App's core noun/verb throughout. |
| transcribe / transcription | **litteroida** / **litterointi** | Standard Finnish term for turning speech into text; never conflated with kääntää. |
| translate / translation | **kääntää** / **kääntäminen** | Kept strictly distinct from litterointi. |
| recording (live capture) | **nauhoitus** | Status line "Recording..." → "Nauhoitetaan..." (verb form). |
| speech engine | **tunnistusmoottori**, short nav form **moottori** | `Settings_Category_SpeechEngine` (category header) uses the shorter **puheentunnistus**; `Settings_Engine_Heading` (page heading, more room) uses the full **puheentunnistusmoottori**. |
| model | **malli** | Already a native Finnish word. |
| runtime | **ajoympäristö** | Compounds with an untranslated identifier: `Vulkan-ajoympäristö`. |
| source language | **lähdekieli** | |
| target language | **kohdekieli** | Also used for `Language_Target_TranslationHint` ("Translation target") rather than inventing a second term. |
| hotkey | **pikanäppäin** | Standard Finnish Windows term for a keyboard shortcut. |
| push-to-talk | **pohjassapito** (noun, "holding down") | Badge: `Pohjassapito`. Mid-sentence lowercase form for the conflict message pre-inflected to illative, see below. |
| toggle | **kytkin** | Badge: `Kytkin`. |
| binding | **sidos** | Not separately surfaced as standalone UI copy in this key set; recorded for glossary completeness. |
| tray | **ilmaisinalue** | Official Windows Finnish term for the notification area. |
| widget | **vimpain** | Matches Windows 11's own Finnish term for "widgets" (`Vimpaimet`). |
| waveform | **aaltomuoto** | Not surfaced as standalone copy in this key set; recorded for completeness. |
| wait time / silence timeout | **odotusaika** (concept), title **Hiljaisuuden aikakatkaisu** | The settings title spells out "silence timeout" literally since it is a fixed heading; body text uses the shorter "odottaa" verb construction. |
| punctuation | **välimerkit** | |
| profanity filter | **kirosanasuodatin** | |
| inject / typed into | **kirjoittaa** ("kirjoitetaan") | One verb for the whole concept; no invented technical term. |
| clipboard | **leikepöytä** | Standard Windows Finnish term. |
| cloud engine / cloud provider | **pilvimoottori** / **pilvipalveluntarjoaja** | Short badge: `Pilvi`. |
| API key | **API-avain** | |
| onboarding | **opastus** | "Parlotype tour" → `Parlotype-opastus`; "Skip tour" → `Ohita opastus`. |
| restart required | **Vaatii uudelleenkäynnistyksen** | Inherently long in Finnish — "uudelleenkäynnistys" itself is 20 letters; no shorter idiomatic alternative exists. |
| build (software) | **versio** | Kept consistent with how "version" is already handled elsewhere; avoids introducing a second word ("koontiversio") for the same thing users see as "Build:" / "development build". |
| prompt (LLM instruction) | **kehote** (plural kehotteet) | Established modern Finnish AI-tooling term, distinct from a "hint" or "tooltip" word. |

## Case-inflection notes — resolved by compounding (no flag needed)

These would have needed Polish/Ukrainian-style restructuring in a language without
Finnish's compounding option; instead, the raw placeholder became an invariant
hyphen-prefix and only the trailing Finnish noun carries the case:

- **Transcribe_Status_RuntimeRestartRequiredFormat** → `"...käyttääksesi
  {0}-ajoympäristöä"` (partitive on `ajoympäristö`, `{0}` untouched).
- **Transcribe_Status_RuntimeUnavailableFormat** → `"{0}-ajoympäristö ei ole
  käytettävissä..."` (nominative subject — no case at all needed here).
- **Settings_Runtime_RestartNoteFormat** → `"...käyttää jo {0}-ajoympäristöä...
  vaihtaaksesi {1}-ajoympäristöön..."` (partitive, then illative, both on
  `ajoympäristö`).
- **Language_Toast_SourceUnsupportedFormat** and the first half of
  **Language_Toast_TargetUnsupportedFormat** → `"{1}-moottorissa"` (inessive on
  `moottori`, not on the engine-name placeholder).

## Case-inflection notes — keys where a raw `{0}` would need a Finnish case suffix

Per the brief, flagging every key where the literal English phrasing would force
Finnish to inflect a placeholder it cannot touch, and how each was restructured:

- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — `{0}` is a
  language name; "to X" wants the illative/allative case on the name itself
  (`"...ksi"`/`"...lle"`), which a raw substituted value cannot take. Restructured to
  **`"Käännä: {0}"`** (label + colon), matching `Language_ToggleSwitch_Translate`'s
  fallback ("Käännä").
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Finnish
  "puhua" takes a partitive object ("puhut suomea"), which the raw nominative
  placeholder can't produce, and the same applies to "types {1}". Restructured to
  **`"Puhut: {0} → Parlotype kirjoittaa: {1}."`**, reusing the arrow the English
  already has and adding a colon after each verb.
- **`Language_Toast_TargetUnsupportedFormat`** ("...Translation set to {2}.") — "set
  to X" wants the translative case (`"-ksi"`) directly on the language name. The
  engine-name half of this key (`{1}`) was solved by compounding (see above); this
  clause was restructured with a label instead: **`"...Käännöskieli: {2}."`**
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — a literal
  translation wants the raw engine name in the genitive as a passive agent
  (`"{0}:n toimesta"`). Rather than force a case suffix onto an unknown value,
  the sentence was flipped from passive-with-agent to active voice:
  **`"...{0} ei tue tätä."`** — `{0}` is now a bare nominative subject, no case
  needed regardless of what value arrives.
- **`Settings_Hotkeys_Gesture_HoldFormat`** ("Hold {0}") and
  **`Settings_Hotkeys_Gesture_DoubleTapFormat`** ("Double-tap {0}") — `{0}` here is
  an already-localized key-name phrase (e.g. "Oikea Ctrl") coming from another key.
  A natural Finnish imperative ("Pidä {0} pohjassa", "Kaksoisnapauta {0}") would make
  that phrase the direct object of a verb needing partitive case, which would in turn
  require inflecting the *adjective* inside it (`Vasen`/`Oikea`) — but those words are
  shared with other contexts where they must stay nominative. Restructured both as
  label-plus-colon: **`"Pidä pohjassa: {0}"`** / **`"Kaksoisnapauta: {0}"`**. This
  composes into `Hotkey_Hint_TalkFormat`/`DictateFormat` less smoothly than a plain
  phrase would (see "Things I was unsure about" below), but keeps every reused
  sub-phrase in a single, always-valid nominative form.

No other key needed this treatment. Two things that made Finnish easier than Polish or
Ukrainian on this front, worth recording for future reference: (1) Finnish predicate
adjectives/participles (`käytettävissä`, `saatavilla`) don't agree in gender with their
subject — Finnish has no grammatical gender — so `Cloud_Error_ProviderUnavailableFormat`
needed no rework at all despite a subject of unknown "gender"; (2) numeric placeholders
(version numbers, HTTP codes, sizes) never visibly inflect in digit form, so none of the
`Settings_Updates_Status_*` or `Cloud_Error_*` keys needed anything beyond plain
substitution.

## Length

Finnish agglutination did make some compounds longer than the English, but nothing
required abandoning a literal rendering for a shorter paraphrase except:

- **`Transcribe_Source_Auto`** ("Auto", narrow language chip) — a bare `"Auto"` in
  Finnish reads as the noun "car" out of context, which would be actively confusing in
  a narrow chip with no surrounding sentence. Used **`"Autom."`** instead (short for
  automaattinen) to keep the chip's brevity without the ambiguity. Elsewhere, where
  there's a full sentence around it (`Settings_Runtime_Auto_Name`, a runtime-selection
  radio option with a description line beside it), the unambiguous **`"Automaattinen"`**
  was used instead — same concept, two renderings, because the failure mode (a
  one-word chip mistaken for "car") only exists in the narrow-chip context.
- **`Onboarding_Widget_Title`** ("The recording widget") — a literal
  "Nauhoitusvimpain-ikkuna" or similar would run long for a tour-screen heading;
  settled on the shorter single compound **`"Tallennusvimpain"`**.
- **`Settings_SilenceTimeout_Title`** ("Silence timeout") — **`"Hiljaisuuden
  aikakatkaisu"`** is inherently ~50% longer than the English because both halves of
  the compound are unavoidable (there's no shorter idiomatic Finnish for "silence
  timeout" as a fixed heading); flagging this as expected Finnish expansion rather
  than a fixable wording problem.
- **`Hotkey_Hint_TalkFormat`** / **`DictateFormat`** / **`WithCancelFormat`** — rather
  than grafting a Finnish purpose clause onto the end of an already colon-terminated
  gesture label (see above), these reuse the **`·`** middle-dot separator the English
  source already uses in `WithCancelFormat`, for all three: `"{0} · puhu"`,
  `"{0} · sanele"`, `"{0} · Esc peruuttaa"`. This keeps every composed tooltip a
  consistent, scannable chain instead of a run-on sentence.

Most body paragraphs (onboarding copy, settings descriptions) ran 15–30% longer than
English, consistent with normal Finnish expansion, but none of them sit in
space-constrained controls, so no shortening was needed there.

## Windows-terminology matches

Used real Finnish Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Tehtävienhallinta**; its Startup tab → **Käynnistys**; "Enable" →
  **Ota käyttöön**.
- File Explorer → **Resurssienhallinta**; Run dialog → **Suorita**-valintaikkuna; Task
  View → **Tehtävänäkymä**; the Ctrl+Alt+Del screen → **Suojausnäyttö**; Windows Voice
  Typing → **Windowsin puhesyöttö**; input method switching → **syöttötavan vaihto**.
- The lock-workstation shortcut is rendered as **Lukitse tietokone**, matching the
  wording on the Windows lock screen itself rather than a literal "lock workstation".

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — I could not verify a single
   fixed official Finnish name for this menu from memory. Used **`"Pikavalikko
   (Win+X)"`** as a readable, disambiguated rendering; flagging for a native check,
   same caveat Ukrainian's glossary raised for the same key.
2. **`Settings_Hotkeys_Reserved_GameBar`** — Finnish Windows may keep "Xbox Game Bar"
   fully in English as a Microsoft product name, or may use a partial translation. Used
   **`"Avaa Xbox Game Bar"`**, keeping the brand name untranslated; worth checking
   against current Microsoft Finnish localization.
3. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used **`"Projisoi
   näyttö"`**; Windows' own Finnish wording for this specific pane may be a single verb
   ("Projisoi") without "näyttö" — flagged for a native check.
4. **`Settings_Category_Input`** ("Input") — translated literally as **`"Syöttö"`**,
   same judgment call Polish's glossary flagged for the same key: generic enough to
   match the English's own vagueness, but worth a second look once the category's
   exact contents are final.
5. **`ModelDownload_ConfirmWithProjectorFormat` / `Settings_Gemma4Model_Description`**
   — "vision projector" (the `mmproj` component) was rendered as **`"näköprojektori"`**,
   a coinage rather than an established Finnish term (I'm not aware of one). Flagging
   in case a more standard Finnish AI/ML term already exists.
6. **`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`** composing into
   **`Hotkey_Hint_TalkFormat`/`DictateFormat`** — see the case-inflection section
   above. The composed result (e.g. `"Pidä pohjassa: Oikea Ctrl · puhu"`) is
   grammatically valid and consistent but reads more like a labeled fact than a single
   flowing instruction. If the maintainer ever revisits key composition, a single
   combined key per hint (rather than composing two independently-formatted pieces)
   would let Finnish (and probably several other languages) phrase this more
   naturally.
7. **`Settings_LlamaCpp_BuildLabel`/`UpdateAvailableBody`/`Backend` "build" → `versio`**
   — chose to reuse "versio" (version) rather than coin a separate word for "build",
   since the English itself already uses "version" and "build" somewhat
   interchangeably in this UI (`Settings_Updates_*` vs `Settings_LlamaCpp_*`). A
   native reviewer might prefer distinguishing them if the two ever appear side by
   side in the same view.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Oletus (sanatarkka litterointi)"** — verbatim/word-for-word rendered as *sanatarkka (word-accurate)*, paired with the already-established *litterointi* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Pidä pohjassa: oikea Ctrl`.**

Side is now lowercase after the colon: `oikea {0}` / `vasen {0}`. `Conflict_AlreadyBoundFormat` quotes the gesture (`”{0}” on jo sidottu {1}.`).
