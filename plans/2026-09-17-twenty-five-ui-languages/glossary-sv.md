---
title: Swedish (sv) translation glossary
status: reference
created: 2026-09-17
---

# Swedish (sv) glossary

Companion to `brief.md`. Choices made for the 389-key Swedish translation in
`<scratch>/sv.json`.

## Register

**Du** (informal), never **Ni/Ers**. This is not a close call in Swedish: "du-reformen"
of the 1960s–70s made *du* the default address form for essentially all software,
consumer or enterprise — Windows itself, Office, and every mainstream Swedish app
address the user as *du*. There is no realistic alternative register to weigh against
it (unlike German's du/Sie choice). Possessives are **din/ditt/dina**, never a formal
substitute. Used consistently in onboarding, tooltips, error text, and settings
descriptions. Imperatives drop the pronoun the same way English does ("Tryck", "Välj",
"Håll") — Swedish imperative verb forms are already the bare stem, so there is no
length cost either way here, unlike German's du/Sie imperative gap.

Where Windows' own feature names are referenced (Aktivitetshanteraren, Utforskaren,
Kör), those are Windows' real Swedish UI names, not literal translations — the sentences
around them stay in Parlotype's own *du* voice.

## Quotation convention

- **”…”** — the same right double quotation mark (U+201D) at both open and close, per
  standard Swedish typography (distinct from German „…“, French «…», or English "…").
  Used for quoted terms, literal UI fragments and substituted names in prose, e.g.
  `”Cloud”`, `”{0}”`. No nested-quote case arose in this key set, so the alternate
  single-mark convention (’…’) was not needed — I normalized the one place the English
  source used straight single quotes (`'{0}'` in `Cloud_BaseUrl_UnsupportedSchemeFormat`)
  to the same ”…” convention for consistency across the file, rather than mixing quote
  styles.
- **En dash `–`** (with plain spaces on both sides) for parenthetical/status clauses,
  mirroring the English source's em dash `—`: `Redo – Spelar in…`,
  `Molnleverantören är inte tillgänglig – försök igen om en stund`. Swedish typography
  uses the en dash, not the em dash, for this role.
- **Non-breaking space (U+00A0)** between a number and a kept unit token — used in
  `Settings_SilenceTimeout_WaitFormat` (`{0} ({1} s)`), matching Swedish
  typographic convention for numbers and units. Sizes inside running prose (`670 MB`,
  `10 GB`) use a plain space, consistent with how the German and Spanish files treat
  prose sizes.
- **Ellipsis `…`** (single U+2026 character, no preceding space) for in-progress
  states — `Läser in modell…`, `Spelar in…` — rather than three literal periods.

## en/ett decisions

The task's four borrowed technical nouns, decided once and held throughout:

| Term | Gender | Definite form | Note |
|---|---|---|---|
| **widget** | en | widgeten | Kept as an English loanword — Swedish has no shorter native term for a small always-on-top panel, and Windows 11's own Swedish UI already uses "widget"/"widgetar" for its taskbar feature. Loanwords ending in a consonant default to **en** in Swedish, which matches here. |
| **prompt** | en | prompten | Kept as a loanword — this is the term Swedish AI/tech coverage and documentation already uses (`en prompt`, plural `promptar`). `Settings_Prompts_Title` → "Promptar". |
| **engine** | — | — | **Not** kept as a loanword. Translated to the native Swedish word **motor** (as in `sökmotor`, `spelmotor`), which is already grammatically **en** (en motor, motorn, motorer). This sidesteps an en/ett decision on the loanword itself, and matches the brief's "prefer native terms when idiomatic" guidance better than an invented or borrowed alternative would. |
| **cloud** | — | — | **Not** kept as a loanword either. Translated to the native Swedish word **moln** (weather-cloud and tech-cloud share the same word in Swedish, as in "molntjänst"), which is **ett** (ett moln, molnet, moln). `Transcribe_CloudBadge` → "Moln"; compounds `molnmotor`/`molnleverantör` inherit **en** gender from their Swedish head noun (`motor`, `leverantör`), independent of `moln`'s own ett-gender — Swedish compound gender always follows the last element. |

Two more loanwords, decided the same way and held consistently:
- **build** (a specific llama-server release, distinct from "version") → kept as the
  English loanword **build**/**builds**, undeclined (`Settings_LlamaCpp_BuildLabel` =
  "Build:", `Settings_LlamaCpp_AvailableBuildsHeading` = "Tillgängliga builds"). This is
  a genuinely different concept from *version* in this app (several llama.cpp builds
  can exist per version number), so it gets its own term rather than reusing *version*.
- **runtime** (Vulkan vs. CPU backend selection) → kept as the loanword **runtime**, en
  (en runtime, runtimen), e.g. `Vulkan-runtimen`. Considered the native alternative
  *körtid*, but that word already means "execution duration" in Swedish and would be
  actively misleading next to a duration-based setting like Väntetid/silence-timeout in
  the same settings page. Microsoft's own Swedish technical documentation also leaves
  "runtime" untranslated (".NET runtime" etc.), which this follows.
- **the app's own "build"** (e.g. "this build was not installed from Setup.exe") is a
  *different* concept from the llama-server build above — it means "this compiled copy
  of Parlotype" — and is translated as **version** (`utvecklingsversion` for
  "development build"), not left as the loanword, since here it maps directly onto the
  ordinary Swedish word for "a build/release of the app" that users already see
  elsewhere in the same Updates page (`Settings_Updates_Status_AvailableFormat` =
  "Version {0}...").
- **backend** → kept as the loanword **backend** (`Settings_Runtime_Description`:
  "Whisper.net-backend"), standard in Swedish developer-facing copy.

## Core glossary

| English term | Swedish | Notes |
|---|---|---|
| dictation | **diktering** | `Onboarding_Recording_Title` → "Börja diktera" (verb: diktera); "kortkommando för diktering" throughout for dictation hotkeys. |
| transcribe | **transkribera** / **transkribering** | Kept distinct from *översätta*. |
| translate | **översätta** / **översättning** | Never used to mean *transcribe*. |
| recording | **inspelning** | Also `Transcribe_Status_Recording` → "Spelar in…". |
| speech engine | **talmotor** | One compound word throughout, e.g. `Settings_Category_SpeechEngine` and `Onboarding_Engine_Title` ("Välj en talmotor"). The bare nav sub-label `Settings_Engine_Title` uses just **"Motor"**, since the category above it already supplies "Tal-". |
| engine (bare) | **motor** | Native Swedish word, not a loanword — see the en/ett table above. |
| model | **modell** | |
| runtime | **runtime** | Loanword, en-gender — see table above. |
| source language | **källspråk** | ett (ett källspråk), following "språk" itself. |
| target language | **målspråk** | ett (ett målspråk). |
| hotkey | **kortkommando** | Matches the word Windows' own Swedish UI uses for keyboard shortcuts (ett kortkommando, kortkommandot, kortkommandon). Used in preference to the more literal "snabbtangent". |
| push-to-talk | **Push to talk** | Kept as the English term, unchanged — this is the fixed term Discord, Windows and most Swedish voice/comms software use verbatim; there is no established native alternative. |
| toggle (mode) | **Växling** (badge) / **växlingsläge** (in a sentence) | The compact badge form `Settings_Hotkeys_Mode_Toggle` is "Växling" to fit the badge width next to "Push to talk"; the same concept spelled out mid-sentence (`Settings_Hotkeys_ModeToggleTooltip`, `Settings_Hotkeys_Conflict_Mode_Toggle`) uses "växlingsläge" so it can't be misread as the unrelated verb "växla" ("switch") that appears in the same sentence. Both forms share the same root and are never used to mean anything else. |
| binding | *(not a separate surfaced term)* | No dedicated `_Binding` key exists in this 389-key set; always expressed via "kortkommando". |
| tray | **meddelandefältet** | The actual Windows Swedish name for the system notification area (distinct from "aktivitetsfältet", the whole taskbar). |
| widget | **widget** | Loanword, en-gender — see table above. |
| waveform | *(no key in this set surfaces this string alone)* | Not present as standalone copy in the 389 keys. |
| wait time | **väntetid** | `Settings_SilenceTimeout_Title` → "Väntetid" (the settings page name itself uses the glossary term directly, rather than a literal "Tystnadsgräns", so the concept has exactly one name everywhere it appears). |
| punctuation | **skiljetecken** | The concrete marks (commas, periods) — used consistently rather than switching to the more abstract "interpunktion". |
| profanity filter | **svordomsfilter** | ett (ett filter); "svordomar" for the masked words themselves throughout. |
| inject / typed into | **skrivas in** | "den igenkända texten skrivs in i…" — natural phrasing per the brief, no invented technical term. |
| clipboard | **Urklipp** | The real Windows Swedish name for the system clipboard (not a literal "klippbord"). |
| cloud engine / cloud provider | **molnmotor** / **molnleverantör** | "moln" translated (not a loanword, see table above); "leverantör" for *provider* throughout, never "provider". |
| API key | **API-nyckel** | |
| onboarding | **rundtur** | Matches the English source's own word choice (`Onboarding_WindowTitle` = "Parlotype tour", not "...onboarding") — rendered as "Parlotype-rundtur", short and native rather than a borrowed "onboarding". |
| restart required | **Omstart krävs** | |

## Windows feature names

Per the brief's "prefer the terms Swedish Windows uses," these use Windows' own Swedish
UI wording rather than literal translations of the English:

- Task Manager → **Aktivitetshanteraren**
- Startup apps tab → **fliken Starta** (the Aktivitetshanteraren tab Windows itself
  calls "Starta")
- File Explorer → **Utforskaren**
- Run dialog → **dialogrutan Kör**
- Task View → **Aktivitetsvy**
- Win+P pane → **Projicera**
- Game Bar → **Xbox Game Bar**
- Win+H → **Windows-röstinmatning**
- Win+L → **Lås datorn**
- Show/hide desktop → **Visa/dölj skrivbordet**

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — I used "Snabblänkmenyn". I
  could not confirm a single fixed official Swedish name for this menu across current
  Windows 11 builds; it is sometimes left as "Windows-menyn" in Microsoft's own Swedish
  documentation. Worth checking against the current build's actual tooltip text.
- **`Settings_Hotkeys_Reserved_SecurityScreen`** (Ctrl+Alt+Del) — I used
  "Säkerhetsskärm". Windows' own Swedish wording for this screen has varied by version
  ("Säkerhetsalternativ" also appears); a native speaker with the current build should
  confirm.
- **`Settings_Hotkeys_Reserved_SwitchInputSource`** (Win+Space) — translated as "Byt
  inmatningsspråk" (switch input *language*) rather than a literal "inmatningskälla"
  (input *source*), since that is what this key actually toggles on Windows and matches
  the wording Windows itself uses for the same shortcut. Flagging the deliberate
  departure from a literal rendering in case the maintainer wants closer parity with
  the English noun.
- **`ModelDownload_ConfirmWithProjectorFormat`** — "vision projector" (the mmproj
  component) doesn't have an established Swedish tech term; I used "synprojektor". This
  is a coinage, not a term users have seen before, and is worth a native-speaker check
  if this string gets much visibility.
- **`Settings_LlamaCpp_Status_NotProbed`** — "Not probed" (no connectivity check has run
  yet) translated as "Inte kontrollerad"; "probed" has no single natural Swedish
  technical equivalent, so this leans on the general sense (not yet checked) rather than
  a literal "avsökt".

## Placeholder / grammatical-agreement note

Swedish nouns are not case-marked the way Russian is, so no key in this set forced a
grammatical-case rephrasing purely to accommodate a substituted `{0}`/`{1}`/`{2}`.
Where Swedish *does* mark agreement — adjective gender/number, or a definite-form
suffix — every placeholder in this set holds a proper noun (an engine name, a model
name, a chord, a version number, a provider name) rather than a common noun an
adjective would need to agree with, so no key was blocked by this either. Kept
substituted values at clause edges (after a colon or a dash) wherever the English
source already did, per the brief's defensive practice — see e.g.
`Language_Source_DetectedFormat` ("Identifierad: {0}") and
`Cloud_NotConfigured_MissingKeyFormat` ("{0}: ingen API-nyckel konfigurerad. …").

The one place gender surfaces without being forced is the compound-noun gender
inheritance noted in the en/ett table above (`molnmotor`/`molnleverantör` taking **en**
from their Swedish head noun regardless of `moln`'s own **ett** gender) — a fact about
Swedish compounding, not a translation problem.

## Validation performed

Programmatically checked `sv.json` against `sv.brief.json`: all 389 keys present with
no extras, every `{0}`/`{1}`/`{2}` placeholder set matches the English source exactly,
the literal template tokens `{speech_lang}` and `{text_lang}` in
`Settings_Prompts_Help_BuiltInBody` (and the two `_Editor_*Hint` / `_Help_*Description`
keys that precede those tokens elsewhere in the prompts-help copy) are preserved
verbatim, and no value over 25 characters matches the English source byte-for-byte.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Standard (ordagrann transkribering)"** — verbatim/word-for-word rendered as *ordagrann (word-for-word)*, paired with the already-established *transkribering* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Håll höger Ctrl`.**

Side is now `höger {0}` / `vänster {0}`, lowercase. `Conflict_AlreadyBoundFormat` quotes the imperative gesture: `”{0}” är redan kopplat till {1}.`
