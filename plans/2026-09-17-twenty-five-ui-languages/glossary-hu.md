---
title: Hungarian (hu) translation glossary
status: reference
created: 2026-09-18
---

# Hungarian (hu) glossary

Companion to the 389-key Hungarian translation (delivered as three slices,
`hu.part1.json`–`hu.part3.json`, merged by the importer). Read `brief.md` first — this
file only records the choices specific to Hungarian.

## Register

Second-person singular, informal, pronoun dropped where the verb ending already carries
it (`"Beszélsz"`, not `"Te beszélsz"`). Buttons and short controls use the plain
imperative, matching Windows' own Hungarian button style (`Mentés`, `Mégse`, `Törlés`,
`Kilépés` — these are actually deverbal nouns/infinitival forms, the way Hungarian
software conventionally labels commands, and are used here for every `_Button` key).
Descriptive body copy uses second-person-singular indicative (`"A program letölti..."`,
`"Ha engedélyezve van..."`) or an impersonal/passive-like construction when the subject
is generic. No `Ön`/`Te` split was needed the way German needs `Sie`/`du` — Hungarian
consumer software (Windows, Office) does not address the user with a distinct polite
pronoun; the plain second-person-singular with dropped pronoun **is** the neutral
register. Sentence case throughout, no exclamation marks, no marketing language.

## Quotation convention

Hungarian typographic quotation marks: **opening low double-9 `„`, closing high
double-6 `”`** — `„Automatikus felismerés”`. Used for quoted UI option names, model
names embedded in sentences, prompt placeholder syntax examples, and the ADR-032
slogan, which stays in English inside the quotes as a citation, matching the other
language files: `(ADR-032, „Local by default. Cloud by choice.”)`.

## Core glossary

| Term | Hungarian | Notes |
|---|---|---|
| dictation | **diktálás** (verb: diktál) | App's core noun/verb throughout. |
| transcribe / transcription | **átirat** (noun) | The established Hungarian term for a written record of speech (court/interview transcripts use the same word). Used loosely as the process noun too (`"A felhőalapú átirat sikertelen volt"`) rather than coining a separate verb, to keep one word for the whole concept. |
| translate / translation | **fordítás** (verb: fordít) | Kept strictly distinct from átirat — no overlap risk, this is the everyday Hungarian word for translate. |
| recording (live capture) | **felvétel** | Status line "Recording..." → `"Felvétel..."` (ellipsis implies in-progress, same pattern Hungarian software uses for `Mentés...`, `Betöltés...`). |
| speech engine | **motor** (short nav form), **beszédfelismerő motor** (full) | `Settings_Category_SpeechEngine` (nav category, narrow) uses **Beszédfelismerés**; `Settings_Engine_Heading` (page heading, more room) uses **Beszédfelismerő motor**; `Settings_Engine_Title` (the nav leaf under the category) is just **Motor**. |
| model | **modell** | Already native-feeling, no alternative needed. |
| runtime | **futtatókörnyezet** | Standard Hungarian IT term (".NET-es futtatókörnyezet" etc). Composes as `{0} futtatókörnyezet` — see the case-suffix section below. |
| source language | **forrásnyelv** | |
| target language | **célnyelv** | Also used for `Language_Target_TranslationHint` ("Translation target") rather than inventing a second term. |
| hotkey | **billentyűparancs** | Windows 11's own Hungarian term for "keyboard shortcut" (Settings → Accessibility → Billentyűparancsok). Preferred over the more generic "gyorsbillentyű". |
| push-to-talk | **nyomva tartás** (noun, "holding down") | Badge: `Nyomva tartás`. |
| toggle | **kapcsoló** | Badge: `Kapcsoló`. |
| binding | **hozzárendelés** | Not separately surfaced as standalone UI copy in this key set; recorded for glossary completeness. |
| tray | **értesítési terület** | Official Windows Hungarian term for the notification area. Long, but only appears in prose, never in a tight badge. |
| widget | **widget** | Kept as a loanword — Windows 11's own Hungarian UI literally calls its widgets panel "Widgetek", so this is the term Hungarian users already know. |
| waveform | **hullámforma** | Not surfaced as standalone copy in this key set; recorded for completeness. |
| wait time / silence timeout | **várakozási idő** (concept), title **Csend időtúllépése** | "X időtúllépése" ("X's timeout") is the standard Hungarian IT pattern for "X timeout" (`munkamenet időtúllépése` = session timeout), so the title is both literal and idiomatic. |
| punctuation | **írásjelek** | |
| profanity filter | **trágár szavak szűrője** | Short form where space is tight: **szűrő**. |
| inject / typed into | **begépel** | One verb for the whole concept ("a szöveg bekerül" as the passive-flavoured alternative when the app, not the user, is the subject); no invented technical term. |
| clipboard | **vágólap** | Standard Windows Hungarian term. |
| cloud engine / cloud provider | **felhőalapú motor** / **felhőszolgáltató** | Short badge: `Felhő`. |
| API key | **API-kulcs** | Hyphenated per Hungarian orthography rules for an acronym plus a native suffix/word (AkH). |
| onboarding | **bemutató** | "Parlotype tour" → `Parlotype bemutató`; "Skip tour" → `Bemutató kihagyása`. |
| restart required | **Újraindítás szükséges** | |
| build (software) | **verzió** | Reused for "build" as well as "version" (same choice Finnish made) — the English itself uses the two words loosely interchangeably across `Settings_Updates_*` and `Settings_LlamaCpp_*`, and Hungarian has no shorter idiomatic word that isn't just a second synonym for the same thing. |
| prompt (LLM instruction) | **prompt** (kept as loanword, plural: promptok) | Hungarian AI-tooling communities already use this as a loanword (`promptok`, `promptolás`) rather than a translated term like "utasítás", which would collide with generic "instruction" copy elsewhere. |

## The Hungarian-specific problem: vowel harmony AND the a/az article

Hungarian marks case with suffixes that obey vowel harmony (front/back), and its
definite article is `a` before a consonant sound, `az` before a vowel sound. A `{0}`
substituted at runtime with a raw value gives no way to choose either — same
double-bind as Finnish, plus the article problem Finnish barely has to deal with
(Finnish has no articles at all).

Three techniques were used, matched to which half of the problem a given key has:

### 1. Case suffix needed on the placeholder itself → juxtapose a native noun after it

Hungarian, like Finnish, lets a foreign/undeclined word sit in front of a native noun
as its modifier, with the case suffix landing only on the native tail word — this is
already how Hungarian handles foreign brand names before a common noun (`a Windows
rendszerben`, not `a Windowsban rendszerben` or any inflection of "Windows" itself).
Because the tail word is always the same native word, its own vowels are known, so the
correct harmony variant is knowable at authoring time:

- `Transcribe_Status_RuntimeRestartRequiredFormat` → `"...a(z) {0} futtatókörnyezet
  használatához"` — the dative suffix lands on `használat` ("use"), not on
  `futtatókörnyezet` or `{0}` at all.
- `Transcribe_Status_RuntimeUnavailableFormat` → `"A(z) {0} futtatókörnyezet nem
  érhető el..."` — nominative subject, no case needed.
- `Settings_Runtime_RestartNoteFormat` → `"...már a(z) {0} futtatókörnyezetet
  használja... hogy átválthass a(z) {1} futtatókörnyezetre..."` — accusative
  (`-et`) then sublative (`-re`), both suffixed onto `futtatókörnyezet`, never onto
  `{0}`/`{1}`.
- `Language_Toast_SourceUnsupportedFormat` → `"...nem forrásnyelv a(z) {1}
  motorban."` — inessive (`-ban`) on `motor`, not on the engine-name placeholder.

### 2. The a/az choice itself → write `a(z)`

Where a definite article has to precede `{0}` directly and no native tail noun is
available to absorb it, Hungarian has a standard written convention for exactly this
uncertainty: `a(z)`, used throughout templates, contracts and software whenever the
following word's initial sound isn't known in advance. This is not an improvised
workaround — it is how Hungarian already writes this exact situation:

- `ModelDownload_ConfirmFormat` → `"Letöltöd az internetről a(z) „{0}” ({1})
  modellt?"` — the quoted name is apposition to `modellt` (accusative on the native
  noun "model"), and `a(z)` covers the article ambiguity for good measure.
- `Settings_Data_DeleteDialog_BodyFormat` → `"...a(z) {1} mappából."`

### 3. Case suffix needed on the placeholder, with no native noun to hang it on →
   restructure with a colon, matching the other three families in this project

Where the value is a bare language name used as a verb's object or after a
preposition — the same shape Polish, Ukrainian, Russian and Finnish all flagged —
Hungarian has no compounding option for a plain object, so the slot was moved behind
a colon or turned into a nominative subject, exactly like the others:

- **`Language_ToggleSwitch_TranslateToFormat`** ("Translate to {0}") — "to X" wants
  the sublative/translative case (`-ra/-re` or `-vá/-vé`) directly on the language
  name. Restructured to **`"Fordítás: {0}"`**.
- **`Language_Summary_Format`** ("You speak {0} → Parlotype types {1}.") — Hungarian
  "beszél" pairs with either an adverbial `-ul/-ül` form or an accusative object,
  both of which need to know the word's own vowels and ending. Restructured to
  **`"Amit beszélsz: {0} → Amit a Parlotype begépel: {1}."`**
- **`Language_Toast_TargetUnsupportedFormat`** ("...Translation set to {2}.") — "set
  to X" wants the translative case (`-vá/-vé`) on the language name. Restructured to
  **`"...Fordítás célnyelve: {2}."`**
- **`Language_Toast_TargetResetFormat`** ("not supported by {0}") — a literal
  translation wants the engine name as a passive agent, which needs a postposition
  (`{0} által`) that itself is fine (postpositions don't inflect), but the sentence
  reads far more naturally in Hungarian as active voice anyway. Flipped to
  **`"...{0} nem támogatja."`** — `{0}` is now a bare nominative subject.
- **`Settings_Hotkeys_Gesture_HoldFormat`** ("Hold {0}") and
  **`Settings_Hotkeys_Gesture_DoubleTapFormat`** ("Double-tap {0}") — `{0}` is an
  already-localized key-name phrase (e.g. "Jobb Ctrl"). A natural imperative
  ("Tartsd lenyomva a {0} billentyűt") would need an accusative suffix on a phrase
  whose final letter is unpredictable, and would also force the "a/az" article
  choice right before it. Restructured to **`"Tartsd lenyomva: {0}"`** /
  **`"Koppints duplán: {0}"`**.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** ("{0} is already bound to
  {1}.") — "bound to X" wants the dative (`-hoz/-hez/-höz`) on the mode name.
  Restructured to **`"{0} már hozzá van rendelve ehhez: {1}."`** — `ehhez` ("to
  this") is a fixed demonstrative that already carries the dative, so `{1}` itself
  stays a bare nominative restatement after the colon.

No other key needed this treatment. `Cloud_Error_*`/`Cloud_NotConfigured_*` needed no
rework — every one already puts the provider-name placeholder first, followed by a
colon, which the English source had already arranged correctly (per the brief's note
that this shape was deliberate).

## Length

Hungarian agglutination pushed some strings longer, but nothing forced abandoning a
literal rendering except:

- **`Settings_SilenceTimeout_Title`** ("Silence timeout") — `"Csend időtúllépése"`
  is about 40% longer than the English; flagged as expected Hungarian expansion for a
  fixed heading, not a fixable wording problem — there is no shorter idiomatic
  Hungarian noun for this concept.
- **`Onboarding_Widget_Title`** ("The recording widget") — used **`"A felvevő
  widget"`** rather than a longer compound; kept short for the tour heading.
- **`Settings_Startup_Title`** and similar single-word nav labels stayed one word
  each (`Indítás`) to match the tight nav-list column width.

Most body paragraphs (onboarding copy, settings descriptions) ran 15–25% longer than
English, consistent with normal Hungarian expansion; none of them sit in
space-constrained controls, so no shortening was needed there.

## Windows-terminology matches

Used real Windows 10/11 Hungarian UI wording rather than literal translations for:

- Task Manager → **Feladatkezelő**; its Startup tab → **Indítás**; "Enable" →
  **Engedélyezés**.
- File Explorer → **Fájlkezelő**; Run dialog → **Futtatás párbeszédpanel**; Task
  View → **Feladatnézet**; the Ctrl+Alt+Del screen → **Biztonsági képernyő**; Windows
  Voice Typing → **Windows hangbevitel**; input method switching → **beviteli mód
  váltása**.
- The lock-workstation shortcut is rendered as **Számítógép zárolása**, matching the
  wording on the Windows lock action itself.
- Notification area (tray) → **értesítési terület**, per the core glossary above.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — no single fixed official
   Hungarian name for this menu recalled with confidence. Used **`"Gyorselérési menü
   (Win+X)"`**, readable and disambiguated; flagging for a native check, same caveat
   Finnish and Ukrainian raised for the same key.
2. **`Settings_Hotkeys_Reserved_GameBar`** — used **`"Xbox Game Bar megnyitása"`**,
   keeping the Microsoft product name untranslated; worth checking against current
   Microsoft Hungarian localization strings.
3. **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — used **`"Kivetítés"`**
   (Windows' short verb for "Project"); flagged for a native check against the exact
   current wording of that specific pane.
4. **`Settings_Category_Input`** ("Input") — translated as **`"Bevitel"`**, generic
   enough to match the English's own vagueness; same judgment call other languages'
   glossaries flagged for this key.
5. **`ModelDownload_ConfirmWithProjectorFormat` / `Settings_Gemma4Model_Description`**
   — "vision projector" (the `mmproj` component) rendered as **`"vizuális
   projektor"`**, a fairly literal coinage rather than an attested Hungarian AI/ML
   term. Flagging in case a more standard term already exists.
6. **`Settings_LlamaCpp_BuildLabel`/`UpdateAvailableBody`/`Backend` "build" →
   `verzió`** — see the core glossary row above; a native reviewer might prefer a
   distinct word if "build" and "version" ever appear side by side in the same view.
7. The **`a(z)`** convention (see above) reads slightly more formal/templated than
   the rest of the terse register. It is standard written Hungarian, but a native
   reviewer may prefer dropping the article outright in the tightest badges/labels
   rather than using `a(z)` — I kept it only in full sentences, never in bare
   labels or badges.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Alapértelmezett (szó szerinti átirat)"** — verbatim/word-for-word rendered as *szó szerinti (word-for-word)*, paired with the already-established *átirat* for transcription; no new terms introduced.
