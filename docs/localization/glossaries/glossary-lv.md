---
title: Latvian (lv) translation glossary
status: reference
created: 2026-09-18
---

# Latvian (lv) glossary

Companion to the 389-key Latvian translation (delivered as two slices, `lv.part1.json` /
`lv.part2.json`, merged by the importer). Read `brief.md` first — this file only records
the choices specific to Latvian.

## Register

**Polite/formal second person**, using the `-iet`/`-ieties` verb endings (the form Latvian
software conventionally uses for direct address — the closest functional analogue to a
V-form; Latvian has no separate `tu`/`jūs` verb-conjugation split the way Russian or German
do, but this ending is what Microsoft's own Latvian Windows/Office localization uses
throughout, e.g. "Ievadiet", "Atlasiet", "Saglabājiet"). The pronoun **`jūs` is dropped**
wherever the verb ending already carries it — matches the terse register the brief asks
for, and matches Polish/Finnish's choice to drop their own pronoun.

Buttons and short imperatives use the **infinitive** form (`Saglabāt`, `Atcelt`, `Aizvērt`,
`Iziet`), matching Windows' own Latvian button style — Latvian infinitives double as neutral
command forms on controls, the same way "Cancel" isn't grammatically marked for person in
English.

Sentence case throughout, no exclamation marks, no marketing language — matches the brief.
`Onboarding_Welcome_Title` ("Welcome to Parlotype") drops the source's implicit enthusiasm
rather than add an exclamation mark: `Laipni lūgti Parlotype`.

## Quotation convention

Latvian typographic quotes: **`„…"`** — opening U+201E (double low-9 quotation mark),
closing U+201D (right double quotation mark, the same shape as English's own closing
quote). This is the same pairing Polish and Czech use, not the German mirrored-closer
style. Checked against Latvian orthography guidance and used throughout for quoted UI
option names, prompt placeholder examples, and the ADR-032 slogan, kept in English inside
the quotes as a citation: `(ADR-032, „Local by default. Cloud by choice.")`.

## Non-breaking space

Used between the number and the unit in `Settings_SilenceTimeout_WaitFormat` →
`"{0} ({1} s)"` — Latvian typography puts a space between a numeral and its unit where
English's source format has none; a non-breaking space keeps `{1} s` from splitting across a
line wrap. No other key needed one — sizes and version numbers arrive pre-formatted as
opaque data (`{1}` already contains `"1.5 GB"`), so I don't control spacing inside them.

## Diacritics

Checked throughout: `ā ē ī ū ļ ķ ņ ģ č š ž` (Latvian does not use `ŗ` in current
orthography — it was dropped from the standard alphabet in 1957 and I did not reintroduce
it). Every long vowel and palatalized consonant in the 389 keys was typed with the correct
mark, not a bare Latin letter.

## Core glossary

| Term | Latvian | Notes |
|---|---|---|
| dictation | **diktēšana** (verb: diktēt) | App's core noun/verb throughout. |
| transcribe / transcription | **transkribēt** / **transkripcija** | Established loanword, distinct from tulkot/tulkošana. |
| translate / translation | **tulkot** / **tulkošana** | Native Latvian term; kept strictly distinct from transkripcija. |
| recording (live capture) | **ierakstīšana** (process) / **ieraksts** (the result) | "Recording..." status → "Ieraksta..." (impersonal present tense, matches "Ielādē modeli..."). |
| speech engine | **runas dzinis**, short form **dzinis** | Plural **dzinēji** (like the established `meklētājdzinis` → `meklētājdzinēji`, "search engine(s)"). |
| model | **modelis** | Already a native Latvian word. |
| runtime | **izpildlaika vide** | See "The Latvian equivalent of the Finnish compounding trick" below — this is how the untranslated runtime name (`Vulkan`, `CPU`) combines with it. |
| source language | **avota valoda** | Matches CAT-tool-standard Latvian localization terminology. |
| target language | **mērķa valoda** | Same source. |
| hotkey | **īsinājumtaustiņš** | Matches Windows' own Latvian term for a keyboard shortcut. |
| push-to-talk | **turēt un runāt** (mode badge) | Gesture instruction is a separate composed key, see case notes. |
| toggle (mode) | **pārslēgt** (badge, infinitive-as-label); **pārslēgšana** (noun form in prose) | |
| binding | **piesaiste** | Not separately surfaced as standalone UI copy in this key set; recorded for glossary completeness. |
| tray | **paziņojumu apgabals** | Official Windows Latvian term for the notification area. |
| widget | **sīklietotne** | Matches Windows 11's own Latvian term for "widgets" (`Sīklietotnes`). |
| waveform | **viļņforma** | Not surfaced as standalone copy in this key set; recorded for completeness. |
| wait time / silence timeout | **gaidīšanas laiks** (concept), title **Klusuma noildze** | "noildze" is the established Latvian IT term for "timeout". |
| punctuation | **pieturzīmes** | |
| profanity filter | **lamuvārdu filtrs** | |
| inject / typed into | **rakstīt** / **tiek uzrakstīts** | Deliberately a *different* verb from `ierakstīt` (recording) — Latvian's `ierakstīt` covers both "record" and "type in" in general usage, and I split them to keep "recording" and "typing into the app" from reading as the same action in a language that has more room to distinguish them than English does. |
| clipboard | **starpliktuve** | Standard Windows Latvian term. |
| cloud engine / cloud provider | **mākoņa dzinis** / **mākoņpakalpojumu sniedzējs** | Badge: **Mākonis**. |
| API key | **API atslēga** | |
| onboarding / tour | **tūre** | "Parlotype tour" → `Parlotype tūre`; "Skip tour" → `Izlaist tūri`. |
| restart required | **Nepieciešams restarts** | `restarts` is an established Latvian IT loanword. |
| build (software) | **versija** | Reused for both "version" and "build" — the English source itself already conflates them (`Settings_Updates_*` says "version", `Settings_LlamaCpp_*` says "build" for the same kind of thing) — same call Finnish made. |
| prompt (LLM instruction) | **uzvedne** (plural uzvednes) | Established modern Latvian AI-tooling term (used this way in Microsoft Copilot's Latvian UI). |
| backend | **aizmugure** | Used once (`Settings_Runtime_Description`, "Whisper.net backend"). No firmly established Latvian IT term for this sense that I'm confident of — flagged below. |

## The Latvian equivalent of the Finnish compounding trick

Finnish resolved several placeholder-case keys by making the untranslatable value the
**first half of a hyphenated compound**, with a native noun carrying the case ending
(`{0}-ajoympäristöä`). Latvian has no compounding option for a foreign proper noun like
that — but it has something that produces the identical result: a foreign brand name used
**attributively, immediately before a native noun**, stays completely undeclined while the
noun after it carries whatever case the sentence needs. This is completely ordinary Latvian
— it's exactly how `Windows operētājsistēma` → genitive `Windows operētājsistēmas` works in
everyday Latvian tech writing; the brand name never inflects, only the following noun does.

I used this for every runtime-name key, with `izpildlaika vide` (feminine) as the carrier
noun:

- `Transcribe_Status_RuntimeRestartRequiredFormat` → `"...lai izmantotu {0} izpildlaika
  vidi"` (accusative `vidi`, `{0}` untouched).
- `Settings_Runtime_RestartNoteFormat` → `"...jau darbojas ar {0} izpildlaika vidi. ...lai
  pārslēgtos uz {1} izpildlaika vidi..."` (`vidi` after `ar` and after `uz`, both times
  only the noun inflects).
- `Transcribe_Status_RuntimeUnavailableFormat` → nominative subject, no case needed at all.

This resolved every runtime-name key **without** needing to flag it as broken — no
restructuring compromise, just ordinary Latvian grammar. I used the same technique for
engine names inside two `Language_Toast_*` keys, with `dzinis` (masculine) as the carrier
noun: `"{1} dzinī"` (locative on `dzinis`, engine name untouched).

## Case-inflection notes — keys where a raw `{0}` would need a Latvian case ending

Flagging every key, per the brief, where the literal English phrasing would force Latvian
to inflect a placeholder it cannot touch, and what I did instead:

- **`Language_ToggleSwitch_TranslateToFormat`** (`Translate to {0}`) — `{0}` is a bare
  language name with no native noun to attach a carrier suffix to (unlike runtime/engine,
  there's no natural "...valodu" to graft the placeholder onto without it reading as if the
  raw value itself were adjectival, which is a stretch for an arbitrary language name the
  way it isn't for a recognized brand name like Vulkan). Restructured with a colon, matching
  every other language that hit this same key: **`"Tulkot: {0}"`**.
- **`Language_Summary_Format`** (`You speak {0} → Parlotype types {1}.`) — Latvian `runāt`
  takes no case-marked object the way this needs, and "types X" has the identical problem.
  Restructured to **`"Runājat: {0} → Parlotype raksta: {1}."`**, reusing the arrow.
- **`Language_Toast_TargetUnsupportedFormat`** (`{0} isn't available in {1}. Translation
  set to {2}.`) — three problems in one key. `{1}` (engine) resolved via the carrier-noun
  trick above. `{0}` (language) as a bare subject would leave the predicate adjective
  `pieejama` agreeing with an unknown gender, so I inserted an explicit classifying noun
  the same way Polish did (`Język {0}` → `Valoda {0}`), which is always feminine and
  carries the agreement instead of the raw value: **`"Valoda {0} nav pieejama {1}
  dzinī."`**. `{2}` ("set to X") was already flagged in the brief's own comment as needing
  a colon guard: **`"Tulkošanas valoda iestatīta: {2}."`**
- **`Language_Toast_SourceUnsupportedFormat`** (`{0} isn't a source in {1}.`) — same two
  fixes together: **`"Valoda {0} nav pieejama kā avota valoda {1} dzinī."`**
- **`Language_Toast_TargetResetFormat`** (`not supported by {0}`) — a literal rendering
  wants `{0}` as a passive agent in the genitive. Flipped to active voice instead, so `{0}`
  is a bare nominative subject regardless of what value arrives: **`"...{0} to
  neatbalsta."`**
- **`Settings_Hotkeys_Gesture_HoldFormat`** (`Hold {0}`) and
  **`Settings_Hotkeys_Gesture_DoubleTapFormat`** (`Double-tap {0}`) — `{0}` here is an
  already-localized gesture phrase from `Modifier_LeftFormat`/`RightFormat` (e.g. `Labais
  Ctrl`), which I kept in the nominative everywhere it's produced. A natural Latvian
  imperative ("turi", "pieskaries divreiz") would make that phrase a direct object needing
  the accusative, which would in turn require inflecting the *adjective* inside it
  (`labais` → `labo`) — but that adjective is shared with other contexts (the standalone
  chip) where it must stay nominative. Restructured both as label-plus-colon:
  **`"Turēt: {0}"`** / **`"Divreiz pieskarties: {0}"`**. This keeps `Modifier_LeftFormat`/
  `RightFormat`'s output valid everywhere it's reused, at the cost of reading more like a
  labeled fact than a flowing instruction — see "Things I was unsure about" below.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** (`{0} is already bound to {1}.`) —
  `{1}` is a mode name (`turēt un runāt` / `pārslēgt`) and "bound to X" wants a case
  Latvian would put on it (dative or `pie` + genitive). Restructured with a colon instead:
  **`"{0} jau ir piesaistīts: {1}."`**

No other key needed this treatment. Two things that made Latvian easier than expected on
this front: (1) present-tense Latvian verbs don't mark gender, so a bare placeholder
leading a sentence as the subject of a present-tense verb (`{0} nevar tulkot`, `{0} rāda
parametru padomus`) never needed rework; (2) quoting a value (`„{0}"`) neutralizes case the
same way it does in Polish and Russian, so every `Language_Toast_ModelPausedFormat`-style
key with a quoted model name needed nothing extra.

## Windows-terminology matches

Used real Latvian Windows 10/11 UI wording rather than literal translations for:

- Task Manager → **Uzdevumu pārvaldnieks**; its Startup apps tab → **Startēšanas
  lietotnes**; "Enable" → **Iespējot**.
- File Explorer → **Failu pārlūks**; the lock-workstation shortcut → **Bloķēt datoru**;
  Task View → **Uzdevumu skats**.

## Things I was unsure about

1. **`Settings_Hotkeys_Reserved_RunDialog`** (Win+R) — used **`"Atvērt dialoglodziņu
   Izpildīt"`**. I recall `Izpildīt` as the traditional Latvian Windows name for the Run
   command, but I'm not fully confident current Windows 11 still uses this exact wording
   rather than `Palaist` — worth a native check.
2. **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — used **`"Ātro saišu
   izvēlne"`** as a readable rendering; I could not verify a single fixed official Latvian
   name for this menu, same caveat Polish and Finnish's glossaries raised for the same key.
3. **`Settings_Hotkeys_Reserved_WindowsSpeechRecognition`** — used a literal
   **`"Windows runas atpazīšana"`**; unsure whether Microsoft ships an official Latvian
   product name for this feature specifically.
4. **`Settings_Runtime_Description`** ("Whisper.net backend") — translated "backend" as
   **`"aizmugure"`**. I'm not confident this is an established Latvian IT term for the
   concept versus an ad hoc literal rendering; flagging for a native review since it only
   appears this one time.
5. **`Settings_Hotkeys_Gesture_HoldFormat`/`DoubleTapFormat`** composing into
   **`Hotkey_Hint_TalkFormat`/`DictateFormat`** — see the case-inflection section above.
   The composed result (e.g. `"Turēt: Labais Ctrl · lai runātu"`) is grammatically sound
   but reads as a labeled fact rather than one flowing instruction, the same tradeoff
   Finnish's glossary flagged for the identical composition problem.
6. **`Settings_Category_Input`** ("Input") — translated as **`"Ievade"`**, the same
   judgment call Polish and Finnish flagged for this key: generic enough to match the
   English's own vagueness, worth a second look once the category's exact contents are
   final.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Noklusējuma (burtiska transkripcija)"** — verbatim/word-for-word rendered as *burtiska (literal)*, paired with the already-established *transkripcija* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Turēt: labais Ctrl`.**

Side is now lowercase after the colon: `labais {0}` / `kreisais {0}`. `Conflict_AlreadyBoundFormat` quotes the gesture.
