---
title: Danish (da) translation glossary
status: reference
created: 2026-09-17
---

# Danish (da) glossary

Translated from English (`da.brief.json`, 389 keys), per `plans/2026-09-17-twenty-five-ui-languages/brief.md`.
Not translated from the parallel Swedish pass — Danish and Swedish diverge exactly in the
technical vocabulary this file is full of, so every choice below was made against the
English source.

## Register

- **Informal `du` throughout.** Danish software is uniformly informal; there is no `du`/`De`
  choice to make in practice — `De` would read as archaic/bureaucratic here. Imperatives
  (`Vælg`, `Slet`, `Gem`) carry most of the direct address; explicit `du`/`din`/`dig` is used
  wherever a full sentence needs a subject ("Du taler", "din genvejstast").
- **Imperative verb forms drop the accent**: `Annuller`, `Installer`, `Aktiver`, `Dupliker`,
  `Rediger`, `Opdater` — matching the plain style Windows' own Danish localization uses for
  buttons. The one exception is `Kopiér` (copy path button): without the accent, "kopier"
  collides with the plural noun "kopier" (copies), and Danish orthography explicitly permits
  the acute accent there to disambiguate. No other imperative in this file needed it.
- **Windows terminology preferred over literal translation** for OS-level concepts: Task
  Manager → *Jobliste*, File Explorer → *Stifinder*, the Run dialog → *dialogboksen Kør*,
  Task View → *Vis opgaver*, Win+X → *Hurtiglinkmenuen*, Xbox Game Bar → *Spillinjen*, Win+L →
  *Lås computeren*, Win+P → *Projicer*. These are the names a Danish Windows user already
  has muscle memory for.

## Quotation marks

**`»…«`** (guillemets, French/Danish style) throughout — the traditional and still
standard Danish convention, used for every quoted app name, search term, or literal phrase
(`Download »{0}« ({1}) fra internettet?`, `Ingen sprog matcher »{0}«.`). Plain ASCII `"`
never appears in the output.

## Core glossary

| Concept | Danish | Notes |
|---|---|---|
| dictation | **diktering** | verb "diktere", noun "diktering" |
| transcribe | **transskribere** | noun "transskribering" — double-s is the correct Danish spelling (cf. "transskription") |
| translate | **oversætte** | noun "oversættelse" — kept strictly distinct from transskribere everywhere |
| recording | **optagelse** | |
| speech engine | **talemotor** | see en/et decision below |
| model | **model** | native Danish word, no change needed |
| runtime | **runtime** | kept as loanword, common gender ("en runtime", "runtimen") |
| source language | **kildesprog** | |
| target language | **målsprog** | mostly appears as "Parlotype skriver" / "target" rather than this literal noun, per the English source's own wording |
| hotkey | **genvejstast** | one word, matches "genvej" (shortcut) + "tast" (key), the term Danish Windows uses for keyboard shortcuts |
| push-to-talk | **hold for at tale** | see note below — no single-word Danish equivalent exists that isn't a raw English borrowing, so a short phrase was chosen and used identically everywhere, including lowercase mid-sentence ("hold for at tale") |
| toggle | **skift** | both as the mode-badge noun and the mid-sentence lowercase form |
| binding | **binding** | borrowed, common gender ("en binding") — only used implicitly (no standalone "binding" string in this brief) |
| tray | **bakke** | "systembakken" in full phrases, "bakken" standalone |
| widget | **widget** | see en/et decision below |
| waveform | **bølgeform** | not present as a standalone string in this brief, but reserved for consistency |
| wait time | **ventetid** | rendered via Settings_SilenceTimeout_* keys as "stilhedstimeout"/wait duration, not a literal standalone string here |
| punctuation | **tegnsætning** | |
| profanity filter | **bandeordsfilter** | "bandeord" = curse words |
| inject / typed into | **skrives ind i** | natural phrasing, no invented technical term, per brief instruction |
| clipboard | **udklipsholder** | standard Danish Windows term |
| cloud engine / cloud provider | **skymotor / skyudbyder** | see "sky" decision below |
| API key | **API-nøgle** | |
| onboarding / tour | **rundvisning** | the English source itself says "tour", not "onboarding", throughout the UI copy — translated consistently as "rundvisning" (guided tour) |
| restart required | **genstart påkrævet** | |
| take effect | **få virkning** | recurring phrase ("Ændringer får virkning ved næste optagelse") — treated as a fixed concept so it doesn't drift into "træde i kraft" in some spots and "få virkning" in others |

## en/et decisions (borrowed technical nouns)

Danish nouns are common gender (**en**) or neuter (**et**); the definite suffix and any
agreeing adjective depend on this. Four nouns needed an explicit call:

- **widget → en widget** (common gender). Definite: *widgetten*. Plural: *widgets*. This
  matches how "widget" is already used in Danish consumer software (phone/desktop widgets).
- **prompt → en prompt** (common gender). Definite: *prompten*. Plural: *prompts*
  ("Prompts" as a nav-category title is the bare plural, unchanged).
- **engine → motor** (native word used instead of borrowing "engine"). *En motor* is
  already common gender in ordinary Danish, so "speech engine" → *en talemotor*, definite
  *talemotoren* (not spelled out in this file since no string needed the definite form).
- **cloud → sky** (native word, "sky" = literal Danish for "cloud"). Chosen over keeping
  "cloud" as a loanword because Danish Windows/OneDrive material already renders "cloud
  storage" as "sky"/"skylagring" ("gem i skyen"), and the brief asks for terms Danish
  Windows already uses. *En sky*, definite *skyen*. Compounds: *skymotor*, *skyudbyder*.
  The short widget badge "Cloud" → **"Sky"** (kept just as short as the English, same
  capitalization pattern).

No agreement problem arose from these four: none of them combines with a substituted
`{0}`/`{1}` placeholder that would need a matching gendered article or adjective ending.

## Placeholder / grammatical-case notes

Danish does not mark grammatical case on nouns (unlike Russian), so the "avoid inflecting a
substituted noun" concern in the brief mostly does not bite here — Danish placeholders sit
safely inline in most sentences. Two places still needed the placeholder pushed to a
sentence edge purely for naturalness, not grammar:

- `Cloud_ProviderName_*` format strings (`Cloud_NotConfigured_MissingKeyFormat` etc.) —
  `{0}` leads, followed by a colon, exactly as the English does. No change needed from the
  source shape.
- `Language_Summary_Format` ("Du taler {0} → Parlotype skriver {1}.") — both placeholders
  already sit after a verb, which is fine in Danish (no case, no agreement).

No key in this batch forced a Danish agreement (gendered article, adjective ending) onto a
substituted `{0}`. Danish's lack of grammatical case makes this a non-issue compared to
Russian.

## Length

Danish compounds are single words (`talemotor`, `skyudbyder`, `bandeordsfilter`,
`stilhedstimeout`), which keeps most labels at or below English length. The one place I
watched closely was the widget's translate-target badge (`Language_ToggleSwitch_Translate` =
"Oversæt", `Language_ToggleSwitch_TranslateToFormat` = "Oversæt til {0}") — both stay
shorter than or equal to the English.

## Unsure / flagged for the maintainer

- **`Settings_Hotkeys_Mode_PushToTalk` / `Settings_Hotkeys_Conflict_Mode_PushToTalk`**
  ("Hold for at tale" / "hold for at tale"): there is no established single-word Danish
  term for "push-to-talk" the way there is for "toggle" (skift). Discord/TeamSpeak-style
  Danish localizations sometimes just keep "Push-to-talk" as an English borrowing instead.
  I chose the phrase over the borrowing because it reads naturally in a lowercase
  mid-sentence position (`Settings_Hotkeys_Conflict_AlreadyBoundFormat`), but a shorter
  badge is worth reconsidering if space ever gets tight in that specific badge slot.
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** ("Project display" → "Projicer"): Windows'
  own Danish name for the Win+P panel is just "Projicer" (a single word, no object), which
  reads slightly abrupt standing alone in a reserved-shortcut list next to fuller phrases
  like "Vis opgaver". Flagging in case the maintainer wants "Projicer skærm" instead for
  parity of phrase length with its neighbors.
- **`Settings_Category_Input`** ("Input" → "Input"): kept as the English loanword rather
  than "Indtastning", since the category groups Hotkeys and reads fine either way; either
  is defensible and I'd defer to whichever the Swedish/German equivalents land on for
  cross-language nav consistency, since these category labels sit side by side in the same
  settings list only within one language, so there's no hard requirement either way.
- **`Settings_LlamaCpp_ManualFolderRequiredError` / `..._PackFolderWarningBody`**: both are
  long, dense sentences (registry/filesystem detail spilling into UI copy already in
  English); the Danish versions are correspondingly long. Not a translation problem, just
  inherited from the source copy being verbose in a settings sub-page with more room.

No key was left untranslated, and no key needed to be paraphrased away from its literal
meaning to fit a grammatical constraint — Danish's lack of case marking meant the
placeholder-agreement problem the brief warns about (the one that hits Russian hard) did
not surface in this batch.
