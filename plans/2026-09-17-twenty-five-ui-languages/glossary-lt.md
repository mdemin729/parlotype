---
title: Lithuanian (lt) translation glossary
status: reference
created: 2026-09-18
---

# Lithuanian (lt) glossary

Companion to the 389-key Lithuanian translation, delivered as two slices
(`lt.part1.json`, `lt.part2.json`, merged by the importer). Read `brief.md` first — this
file only records the choices specific to Lithuanian.

## Register

**Formal, second-person plural verb forms with the pronoun dropped** — imperatives
ending `-kite`/`-(ki)te` (`Pasirinkite`, `Įveskite`, `Atidarykite`) and descriptive verbs
ending `-ate`/`-ote` (`kalbate`, `norite`). This is the only address form Lithuanian
desktop software actually uses: unlike Polish or German, Lithuanian consumer UI does not
have an established informal register to choose against — Microsoft's own Lithuanian
localization (Windows, Office) uses this plural-imperative form uniformly, and Parlotype
follows it rather than inventing a more casual voice the platform doesn't have.

Buttons and short controls use the plain imperative or a bare noun (`Išsaugoti`,
`Atšaukti`, `Atidaryti`), matching Windows' own button style. Sentence case throughout,
no exclamation marks, no marketing language.

A useful side effect for placeholder handling: **Lithuanian present-tense verbs do not
mark grammatical gender** (unlike Polish's past tense). A raw, ungendered placeholder used
as the subject of a present-tense verb — `{0} negali versti` ("{0} can't translate") —
needed no rework at all, the same advantage Finnish's glossary recorded for its own
gender-free grammar.

## Quotation convention

Lithuanian standard quotation marks: **„…“** (opening low-9, closing high-6) — e.g.
`„Large v3 Turbo“`. Checked and used consistently; never `"…"` straight quotes, never
`«…»` (Russian's convention, not Lithuanian's).

ADR-032's positioning line is kept in English inside the quotes, as a citation of the
ADR's own title rather than ordinary prose: `(ADR-032, „Local by default. Cloud by
choice.“)`.

## Diacritics

Checked throughout: `ą ę į ų ū ė č š ž` (and their capitals `Ą Ę Į Ų Ū Ė Č Š Ž`) render
correctly in both output files — verified by round-tripping the JSON through UTF-8 and
spot-checking words that use each one (`įrašoma`, `keiksmažodžių`, `piūklai`-class vowel
length distinctions between `ū`/`u`, `žymeklis`, `švelnesnis`).

## Non-breaking space

Inserted between a number and the unit `s` in `Settings_SilenceTimeout_WaitFormat`
(`"{0} ({1} s)"` — U+00A0 before `s`, unlike the English's tight `{1}s`), matching
standard Lithuanian typographic practice of separating a numeral from its unit. `GB`/`MB`
inside fixed descriptive sentences (`~670 MB`, `~10 GB`) keep the English abbreviation
verbatim per the never-translate register, with the plain space the English source
already uses.

## Core glossary

| Term | Lithuanian | Notes |
|---|---|---|
| dictation | **diktavimas** (verb: diktuoti) | App's core noun/verb throughout. |
| transcribe / transcription | **transkribuoti** / **transkripcija** | Established loanword for turning speech into text (used for interview/recording transcripts); kept strictly distinct from *versti*. |
| translate / translation | **versti** / **vertimas** | Native term, never conflated with *transkribuoti*. |
| recording (live capture) | **įrašymas**; status line uses the impersonal **„Įrašoma...“** | Impersonal reflexive forms (`Įrašoma`, `Kraunama`, `Siunčiama`) are standard in Lithuanian software for progress states and sidestep any person/gender marking. |
| speech engine | **kalbos atpažinimo variklis**; short nav/category forms **variklis** / **kalbos atpažinimas** | Full form on the engine-selection heading (`Settings_Engine_Heading`); the nav label (`Settings_Engine_Title`) is just `Variklis`; the category header (`Settings_Category_SpeechEngine`) is `Kalbos atpažinimas`. |
| model | **modelis** | Already a native/international Lithuanian word, unchanged. |
| runtime | **vykdymo aplinka** | See "The placeholder Lithuanian can't inflect" below — combined with an untranslated identifier as two words, e.g. `Vulkan vykdymo aplinka`, not hyphenated. |
| source language | **šaltinio kalba** | |
| target language | **tikslo kalba** | Parallel genitive-phrase construction with *šaltinio kalba*, distinct from *vertimo kalba* (translation language), which only applies while translation is actually on. |
| hotkey | **spartusis klavišas** | Standard Lithuanian Windows/Office term for a keyboard shortcut (plural `spartieji klavišai`). |
| push-to-talk | **laikymas** (badge/noun); "hold **key**" instructions use **laikyti** | Noun badge parallels *perjungimas* below. |
| toggle | **perjungimas** (badge/noun); verb **perjungti** | |
| binding | **susiejimas** | One configured gesture-plus-mode pair; not heavily surfaced as standalone copy in this key set, recorded for completeness. |
| tray | **pranešimų sritis** | Actual Windows 10/11 Lithuanian term for the notification area (not a literal "tray" translation). |
| widget | **valdiklis** | Matches Windows 11's own Lithuanian term for its Widgets panel (`Valdikliai`); reused here for Parlotype's floating window. |
| waveform | **garso bangos vaizdas** | Not surfaced as standalone copy in this key set; recorded for completeness. |
| wait time / silence timeout | **tylos trukmė** | Settings heading `Settings_SilenceTimeout_Title` → `Tylos trukmė`; body text spells out the "how long to wait" idea in full sentences. |
| punctuation | **skyryba** | |
| profanity filter | **keiksmažodžių filtras** | *keiksmažodžiai* (swear words) is the natural Lithuanian term, more idiomatic than a literal "non-censored words" coinage. |
| inject / typed into | **įvedama** (į) | One verb for the whole concept — "the text is typed into the app" → "tekstas įvedamas programoje/ten, kur..." No invented technical term. |
| clipboard | **mainų sritis** | Official Windows Lithuanian term for the system clipboard. |
| cloud engine / cloud provider | **debesijos variklis** / **debesijos paslaugų teikėjas** | *debesija* is the established Lithuanian IT term for "cloud" computing. Short badge: `Debesija`. |
| API key | **API raktas** | "API" kept as the universal acronym, per established Lithuanian IT usage. |
| onboarding | **apžvalga** | Reused for both the concept and "Parlotype tour" → `Parlotype apžvalga`. |
| restart required | **Reikia paleisti iš naujo** (heading); noun **paleidimas iš naujo** | |
| build (Parlotype's own update) | **versija** | Kept consistent with how "version" is already handled in the Updates section. |
| build (llama.cpp download variant) | **surinkimas** | Deliberately a *different* word from *versija* — llama.cpp builds (CPU/CUDA/Vulkan variants) are a different concept from Parlotype's own release version, and collapsing them into one term would blur two things the Settings → llama.cpp page and the Settings → Updates page both need to say clearly. |
| prompt (LLM instruction) | **promptas** (plural *promptai*) | Established loanword in Lithuanian AI-tooling usage, kept rather than coining a native alternative, matching Polish's and others' precedent. |

## The placeholder Lithuanian can't inflect

Lithuanian has seven cases and marks them on essentially every noun, adjective and much
of its participle system — the same problem Polish, Ukrainian and Finnish flagged. Two
techniques were available, per the task brief, and **both** were used, chosen per key:

**1. Finnish-style prefix, Lithuanian-style (no hyphen, two words):** where the
placeholder attaches naturally to a *native* Lithuanian noun as a foreign attributive
modifier — the same pattern already used productively in real Lithuanian IT prose
(`Windows sistema`, `Chrome naršyklėje`) — only the native noun inflects and the
placeholder stays exactly as it arrived:

- **`Transcribe_Status_RuntimeRestartRequiredFormat`** ("use the {0} runtime") →
  `"...kad galėtumėte naudoti {0} vykdymo aplinką"` (accusative `-ą` on `aplinka`, `{0}`
  untouched).
- **`Transcribe_Status_RuntimeUnavailableFormat`** ("{0} runtime not available") →
  `"{0} vykdymo aplinka nepasiekiama..."` (nominative — no case needed at all, and the
  predicate adjective `nepasiekiama` agrees with the feminine noun *aplinka*, not with
  the placeholder, so its unknown "gender" never matters).
- **`Settings_Runtime_RestartNoteFormat`** (two runtime names) →
  `"...jau naudoja {0} vykdymo aplinką... kad perjungtumėte į {1} vykdymo aplinką..."`
  (accusative both times, on *aplinka* both times).

**2. Colon-label restructuring**, used wherever the placeholder itself is the thing being
named (a language name as the direct object of "speak"/"translate to", or a value being
"set to" something) rather than a modifier of some other concept, so there is no natural
native noun to attach it to:

| key | English | Lithuanian shape |
|---|---|---|
| `Language_ToggleSwitch_TranslateToFormat` | `Translate to {0}` | `Vertimo kalba: {0}` |
| `Language_Summary_Format` | `You speak {0} → Parlotype types {1}.` | `Kalbate: {0} → Parlotype įveda: {1}.` |
| `Language_Toast_SourceUnsupportedFormat` | `{0} isn't a source in {1}. Using your keyboard layout.` | `Kalba {0} nėra šaltinio kalba. Variklis: {1}. Naudojamas klaviatūros išdėstymas.` |
| `Language_Toast_TargetUnsupportedFormat` | `{0} isn't available in {1}. Translation set to {2}.` | `Kalba {0} nepasiekiama. Variklis: {1}. Vertimo kalba: {2}.` |
| `Language_Toast_TargetResetFormat` | `...not supported by {0}.` | Flipped passive→active, Finnish-style: `...{0} to nepalaiko.` (`{0}` becomes a bare nominative subject instead of a genitive/instrumental passive agent — no case possible regardless of what value arrives.) |

The first two of these — `Language_Summary_Format` and
`Language_ToggleSwitch_TranslateToFormat` — are exactly the pair the localization skill's
own convergence table calls out: four independent languages (fr, uk, pl, fi) already
flagged them and fixed them the same way. Lithuanian is a fifth data point for the same
two keys, for the same reason: **Kalbate {0}** and **Versti į {0}** are not grammatical
Lithuanian (the verbs want a case-inflected object the raw placeholder cannot supply),
so both were moved behind a colon.

`Language_Toast_SourceUnsupportedFormat` and `Language_Toast_TargetUnsupportedFormat`
additionally use the **explicit-classifying-noun trick from the Polish glossary**:
prefixing the placeholder with `Kalba {0}` gives the sentence a known-gender (feminine)
grammatical subject, so the predicate (`nėra šaltinio kalba`, `nepasiekiama`) can agree
correctly no matter what the substituted language name actually is.

**Not flagged, despite holding a name:** anywhere the value is already quoted
(`„{0}“` — quoting neutralizes case in Lithuanian exactly as it does in Polish's `„{0}”`
and Russian's `«{0}»`), anywhere a name leads a present-tense sentence as a bare nominative
subject (`{0} negali versti...` — no gender marking on Lithuanian present-tense verbs, no
case marking on a nominative subject), the `Cloud_*` family (every format already puts
the provider-name slot first, followed by a colon, by design — see the key's own
comment), and version/size/path placeholders, which are technical strings a Lithuanian
speaker does not grammatically inflect either.

**Key names** (`Ctrl`, `Right Ctrl`, …) are a separate case: `Settings_Hotkeys_Gesture_
HoldFormat` ("Hold {0}") keeps `{0}` as a bare object of `Laikykite`, with no case ending
applied to it. This is not a workaround — Lithuanian treats indeclinable borrowed technical
tokens this way generally (the way `taksi` never changes shape either), and the brief
requires key names to stay exactly as printed on the keyboard regardless, so there was
never a second option to weigh here.

## Windows-terminology matches

Used real Lithuanian Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Užduočių tvarkytuvas**; its Startup apps tab → **Paleisties
  programos**; "Enable" → **Įjungti**.
- File Explorer → **Failų naršyklė**; the Run dialog → **langas „Vykdyti“**; Task View →
  **Užduočių peržiūra**; the Ctrl+Alt+Del screen → **Windows saugos ekranas**; Windows
  Voice Typing → **Windows balso įvestis**.
- The lock-workstation shortcut is rendered as **Užrakinti kompiuterį**, matching the verb
  Windows itself shows on the lock screen rather than a literal "lock workstation".

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — could not verify a single fixed
   official Lithuanian name for this menu from memory. Used **`Sparčiosios nuorodos meniu
   (Win+X)`**, a readable disambiguated rendering; flagging for a native check, the same
   caveat Finnish's and Ukrainian's glossaries raised for the same key.
2. **`Settings_Hotkeys_Reserved_GameBar`** — kept **`Xbox Game Bar`** untranslated as a
   Microsoft product name (`Atidaryti Xbox Game Bar`); worth checking against current
   Microsoft Lithuanian localization, which may render it differently.
3. **`Settings_Category_Input`** ("Input") — translated as **`Įvestis`**, the same generic
   judgment call Polish's and Finnish's glossaries flagged for this key: broad enough to
   match the English's own vagueness, worth a second look once the category's exact
   contents are final.
4. **`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`** composing into
   **`Hotkey_Hint_TalkFormat`/`DictateFormat`/`WithCancelFormat`** — `Laikykite {0}` /
   `Sparčiai paspauskite du kartus {0}` read correctly but, like Finnish found, compose
   into the tooltip chain (`{0} · kalbėti`) as a labeled fact rather than one flowing
   instruction. Grammatically sound, not maximally natural; a single combined hint key
   (rather than composing two independently formatted pieces) would let Lithuanian phrase
   this more fluidly if the maintainer ever revisits key composition.
5. **`Settings_LlamaCpp_Backend_Unknown` / general "backend"** — the literal word "backend"
   does not appear as translatable UI prose anywhere in these two slices (only backend
   *names* like CPU/CUDA/Vulkan, which stay untranslated per the register), so no glossary
   term was needed for it here; flagging only so a future slice that does surface the bare
   word "backend" doesn't have to invent a term without checking this file first.
6. **`surinkimas` vs `versija` for "build"** — see the core glossary row above. A native
   reviewer might prefer a single word if Parlotype's own build/version concept and
   llama.cpp's build/version concept are ever shown side by side in one view.
