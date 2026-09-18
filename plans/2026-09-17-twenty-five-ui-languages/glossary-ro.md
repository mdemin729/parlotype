---
title: Romanian glossary — core terms, register and conventions
status: reference
created: 2026-09-18
---

# Romanian (ro) glossary

Companion to `ro.brief.part1.json` / `ro.brief.part2.json` and the resulting
`ro.part1.json` / `ro.part2.json`. 389 keys translated across two slices.

## Formality register

**Informal "tu"**, matching how Windows 11 and Microsoft 365's Romanian consumer UI
address the user (e.g. "Particularizează PC-ul tău"), and how Discord and most Romanian
desktop apps address the user. Imperatives use the *tu* form throughout ("Alege",
"Adaugă", "Ține apăsată"), never the formal *dumneavoastră*. Never mixed.

## Quotation convention

**Low-high double quotes `„…”`** — the Romanian standard (opening quote on the baseline,
closing quote at cap-height), used for every quoted phrase and prompt example
(`„{0}”`, `„apoi tradu în ...”`). No nested quoting was needed anywhere in this set.

## Diacritics

**Comma-below forms**: ș (U+0219) and ț (U+021B), never the cedilla forms ş (U+015F) /
ţ (U+0163) that some legacy fonts substitute. Verified programmatically — no cedilla
codepoint appears anywhere in either output file.

## Units and spacing

A plain space before a unit (`{1} s`), matching the Italian/Spanish precedent — I found no
evidence Romanian typography demands a non-breaking space here specifically, and a plain
space keeps the JSON simple; the importer is free to normalize if it prefers ` `.

## Core glossary

| Term | Romanian | Notes |
|---|---|---|
| dictation | **dictare** | |
| transcribe / transcription | **a transcrie** / **transcriere** | |
| translate / translation | **a traduce** / **traducere** | Kept strictly distinct from *transcriere* per the brief. |
| recording | **înregistrare** | |
| speech engine | **motor de recunoaștere** (full: *motor de recunoaștere vocală*) | Category/heading use the fuller form once, nav `Title` uses **Motor** alone, mirroring the English Title/Heading split. |
| model | **model** | |
| runtime | **runtime** (borrowed, kept as-is; definite form *runtime-ul*) | See "The definite-article suffix" below — this is where it bites. |
| source language | **limba sursă** | |
| target language | **limba țintă** | |
| hotkey | **comandă rapidă** (plural *comenzi rapide*) | The term Romanian Windows uses for a keyboard shortcut (`Comenzi rapide de la tastatură`); no shorter native alternative exists that isn't a neologism. |
| push-to-talk | **Apăsare continuă** (badge); **apăsare continuă** mid-sentence | Descriptive rather than a loan — "hold to talk". |
| toggle (activation mode) | **Comutare** (badge); **comutare** mid-sentence | |
| binding | *(not surfaced as its own translated string in this file)* | |
| tray | **zona de notificare** | The official Windows Romanian term for the system tray/notification area. |
| widget | **widget** (borrowed, neuter — *widgetul*) | |
| waveform | *(not surfaced as its own string in this set)* | |
| wait time | **timp de așteptare** | Also used for `Settings_SilenceTimeout_Title`, since "silence timeout" and "wait time" are the same concept per the brief's glossary. |
| punctuation | **punctuație** | |
| profanity filter | **filtru de limbaj vulgar** | *limbaj vulgar* is the natural Romanian equivalent of "profanity"; shorter than the more formal *limbaj licențios*. |
| inject / typed into | **scris în** | Natural phrasing ("textul este scris în aplicație"), no invented technical term. |
| clipboard | **Clipboard** (capitalized, borrowed — Romanian Windows keeps this untranslated) | |
| cloud engine / provider | **motor cloud** / **furnizor cloud** | "Cloud" itself is kept as the badge text (see below). |
| API key | **cheie API** (fem. — *cheia API*) | |
| onboarding | **tur** (e.g. *"Tur Parlotype"*) | Short form; avoids the longer *tur ghidat* except where the English itself says "guided tour". |
| restart required | **Repornire necesară** | |

### "Cloud" badge — kept untranslated

`Transcribe_CloudBadge` stays **"Cloud"**. Romanian tech vocabulary uses "cloud" as a bare,
unmarked loanword at least as commonly as English speakers do ("stocare în cloud", "cloud
computing"); a native equivalent like *"Nor"* would read as unusual for this specific badge
context and risks confusion with the unrelated common noun *nor* (cloud, weather). Every
other `cloud`-adjacent term (*motor cloud*, *furnizor cloud*) keeps the loanword as a
modifier rather than fully nativizing it, for the same reason.

## The definite-article suffix — Romanian's placeholder problem

Romanian marks definiteness with a suffix on the noun (*motor* → *motorul*, "the engine"),
not a separate article word. A raw `{0}` value can never carry that suffix, so "the {0}
runtime" cannot become `{0}-ul` — the hyphenated article would be glued onto whatever
opaque string the placeholder holds (a name like "Vulkan" or "CPU"), which is exactly the
Slavic/Finnish case-suffix problem, just with an article instead of a case ending.

The fix used throughout this file: **put the native noun with its own definite article
before the placeholder**, so the article attaches to `runtime`, never to `{0}`:

| key | English | Romanian |
|---|---|---|
| `Transcribe_Status_RuntimeRestartRequiredFormat` | "Restart Parlotype to use the {0} runtime" | "Repornește Parlotype pentru a folosi **runtime-ul** {0}" |
| `Transcribe_Status_RuntimeUnavailableFormat` | "{0} runtime not available — change in Settings" | "**Runtime-ul** {0} nu este disponibil — schimbă-l din Setări" (reordered — English leads with `{0}`, Romanian needs the noun+article first) |
| `Settings_Runtime_RestartNoteFormat` | "This session is already running the {0} runtime. ... restart Parlotype to switch to {1}" | "Această sesiune rulează deja pe **runtime-ul** {0}. ... repornește Parlotype pentru a trece la {1}" (only the first slot collides with the article; the second, "switch to {1}", is a bare prepositional object and needs no article) |

These three are the only keys in either slice where the collision was real and forced a
reorder rather than a simple word choice. I checked every other `{0}`-leads-a-common-noun
pattern (`Language_UnavailableNoteFormat`'s `{0} can't translate`, the `Cloud_*` error
formats' `{0}: ...`) and none of them needed the article in the first place — engine and
provider names function as proper nouns / labels in these sentences ("Whisper nu poate
traduce", not "*Whisper-ul* nu poate traduce"), so no suffix was ever wanted there. The
`Cloud_ProviderName_*` keys are deliberately bare indefinite noun phrases ("furnizor
compatibil OpenAI") for exactly this reason — they lead every `Cloud_*` format as a label,
never as a definite noun, matching the English comment's intent that no language should
need to inflect them.

Two more numeric placeholders that look similar but are **not** a collision, since a
number never receives Romanian's noun-article suffix — the article sits on the preceding
native noun and the number simply follows it, e.g. `Onboarding_Progress_Format` → "Pasul
{0} din {1}" (*pasul* = "the step", already definite; {0} is just the digit) and
`Settings_Updates_Status_AvailableFormat` → "Versiunea {0} este disponibilă" (*versiunea* =
"the version"). No rephrasing was needed for either.

### Language-name slots — colon convention, not strictly forced

`Language_Summary_Format`, `Language_ToggleSwitch_TranslateToFormat` and
`Language_Toast_TargetUnsupportedFormat` are the three keys every other language in this
project flagged (per the localization skill's cross-language note), because a substituted
language name would need case marking in Slavic/Finnic languages. Romanian barely
case-marks common nouns, so `Tradu în {0}` or `Vorbești {0}` would have been grammatically
fine as-is. I still adopted the **colon convention** used by the shipped Italian/Spanish
files (`Language_Summary_Format` → "Vorbești: {0} → Parlotype scrie: {1}.";
`Language_ToggleSwitch_TranslateToFormat` → "Tradu în: {0}") for two reasons: it reads more
naturally before a language name that arrives as a bare ICU display name with no article of
its own, and it keeps this Romance triad visually consistent with the two already-shipped
Romance languages. This is a style choice, not a forced restructuring — unlike the three
runtime keys above, Romanian was never ungrammatical here.

## Windows/macOS feature names used verbatim from the OS

Per the brief's instruction to use the wording the user's own operating system uses for
reserved-shortcut descriptions:

- Task Manager → **Managerul de activități**; its Startup apps tab → **Aplicații de
  pornire**; its enable action → **Activare** (used consistently across `Settings_Startup_*`
  and `Settings_Hotkeys_Reserved_*`).
- File Explorer → **Explorer**; the Run dialog → **fereastra Executare**; Task View →
  **Vizualizare activități**; Windows Speech Recognition → **Recunoașterea vocală
  Windows**; Windows' own dictation feature → **Dictarea vocală Windows**.
- Xbox Game Bar is kept as **Game Bar** (the product name, as Romanian Windows itself
  does).

## Never-translate register

Checked every string against brief.md's register: Parlotype, Whisper, Parakeet / Parakeet
TDT v3, Gemma 4, sherpa-onnx, llama.cpp, llama-server, Silero VAD, Vulkan, ONNX, OpenAI,
Groq, xAI, Grok, `settings.json`, `secrets.json`, `%LOCALAPPDATA%`, GB/MB/ms, `HKCU Run`,
`Setup.exe`, `api.github.com`, `vulkan-1.dll`, CUDA, mmproj, E4B/E2B, INT8, fp32,
Medium/Large v1/v2/v3, and Ctrl/Alt/Shift/Space/Esc — all survived untouched. The
`{speech_lang}` / `{text_lang}` named tokens in `Settings_Prompts_Help_BuiltInBody` are
copied verbatim (verified programmatically — both tokens present, byte-for-byte, in the
output).

## Gender agreement and placeholders — otherwise clean

Beyond the runtime-suffix collision above, I checked every other `{0}`/`{1}`/`{2}` slot for
gendered-adjective agreement problems. The two noun categories most often substituted here
— language names (used as bare labels or after "vorbești"/"tradu în", never with an
agreeing adjective) and engine/model/runtime names (used as fixed-gender category nouns:
*motorul*, *modelul*, *runtime-ul* — all masculine/neuter regardless of which name fills
the slot) — never forced an agreement choice that depends on the substituted value. No
other key needed English rephrased for a Romanian-specific agreement problem.

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` ("Left {0}" / "Right {0}") render as
postposed **"{0} stânga"** / **"{0} dreapta"** — noun-then-modifier order, matching how
Romanian normally places a locational qualifier after the key name ("Ctrl stânga", "Shift
dreapta"). This works because the fixed set of values filling `{0}` here (Ctrl, Alt, Shift,
Win) are all treated as invariant borrowed tokens, never needing gender agreement.

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (the Win+X menu): translated as "Meniul de
  legături rapide". There is no single authoritative Romanian Windows name for this the way
  there is for "Managerul de activități" or "Explorer" — Microsoft's own Romanian support
  content is inconsistent here.
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P): translated as "Proiectare afișaj".
  The Windows quick-panel title itself is just "Proiectare", which felt too bare standing
  alone in a list next to full phrases like "Deschide Explorer".
- **`Settings_Prompts_Help_BuiltInBody`**: the embedded example "speech in X into X text"
  became „vorbire în X în text în X” — a literal structural rendering of an
  English placeholder-pattern example (it explains the *shape* of a built-in prompt
  template, not a standalone claim), matching how the Italian file handled the same
  sentence. Worth a native read-through given how compressed it is.
- **`Settings_Prompts_CopySuffixFormat`** ("{0} (copy)" → "{0} (copie)"): per
  `PromptSettingsViewModel`'s documented exemption in the localization skill, this string
  becomes user data once written (a saved prompt name) and is not expected to re-translate
  on a later language switch — noting it here so the maintainer doesn't mistake that for a
  live-switch bug specific to Romanian.
- Whether **"motor de recunoaștere"** (my short/working choice for "speech engine" without
  the word *vocală*) reads unambiguously wherever it appears on its own — I kept it short
  everywhere for consistency and the brief's compactness rule, at the cost of dropping
  "vocală" after the first full-form use in Onboarding.

## Verification performed

- Both output files parsed as flat JSON with no wrapper, matched 1:1 against their brief's
  key set (195 + 194 = 389), with zero missing keys and zero overlap between the two files.
- Every `{0}`/`{1}`/`{2}` placeholder set matches its brief entry exactly, per key.
- `{speech_lang}` and `{text_lang}` confirmed present verbatim in
  `Settings_Prompts_Help_BuiltInBody`.
- No translated value over 25 characters is byte-identical to its English source.
- No cedilla-form diacritic (ş U+015F, ţ U+0163) appears anywhere in either file — only the
  comma-below forms (ș U+0219, ț U+021B).
