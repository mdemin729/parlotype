---
title: Greek (el) translation glossary
status: reference
created: 2026-09-18
---

# Greek (el) glossary

Companion to `brief.md`. Choices made for the 389-key Greek translation in
`<scratch>/el.part1.json` / `el.part2.json` / `el.part3.json`. Greek is the first
non-Latin, non-Cyrillic script in this project — full monotonic accentuation is used
throughout (every polysyllabic word carries its tonos; a Greek UI without accents reads
as broken, and a wrong accent can change meaning, e.g. `πότε` "when" vs `ποτέ` "never").

## Register

**Impersonal noun forms for buttons/commands, polite second-person-plural verb forms
(`-τε` ending, no stated pronoun) for instructions and descriptions** — matching how
Windows' own Greek localization works. Buttons and menu commands use the Greek
verbal-noun convention Windows itself uses: `Άνοιγμα` (Open), `Αποθήκευση` (Save),
`Ακύρωση` (Cancel), `Κλείσιμο` (Close), `Διαγραφή` (Delete) — not imperative verbs.
Instructional sentences use the polite plural imperative/present: `Επιλέξτε...`,
`Κρατήστε...`, `Μπορείτε να...`. Possessive "your" is `σας` throughout, never `σου`.
This mirrors Ukrainian's approach of avoiding an explicit pronoun while still committing
to one formality level, adapted to Greek's own button-vs-sentence convention.

## Quotation and typography

- **Guillemets** `«…»`, no inner spacing — `«{0}»`, `«μετά μεταφράστε στα ...»`.
- **Em dash** `—` with plain spaces on both sides, mirroring the English source's
  dash-separated clauses.
- Greek's own question mark `;` is not used anywhere in this file — every key that
  needed punctuation used a colon, full stop or the em dash instead; none of the 389
  strings required a question in running Greek prose (the two literal `?` in
  `ModelDownload_ConfirmFormat` / `Settings_Data_DeleteDialog_Title` keep the ASCII `?`,
  matching how Greek renders a direct yes/no dialog question in software UI).
- SI unit symbols (`s`, `GB`, `MB`, `ms`) are kept as the international symbol, not
  transliterated — this matches Greek scientific/technical usage, where `s` for seconds
  is the same symbol as in English.

## Never-translate register

Followed exactly as specified in `brief.md`: `Parlotype`, `Whisper`, `Parakeet`,
`Parakeet TDT v3`, `Parakeet v3`, `Gemma 4`, `sherpa-onnx`, `llama.cpp`, `llama-server`,
`Silero VAD`, `Vulkan`, `ONNX`, `OpenAI`, `Groq`, `xAI`, `Grok`,
`gpt-4o-mini-transcribe`, `whisper-large-v3`, `Large v3 Turbo`,
`Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc`/`Tab`/`Enter`, `settings.json`,
`secrets.json`, `%LOCALAPPDATA%`, `Setup.exe`, `vulkan-1.dll`, `llama-server.exe`,
`HKCU Run`, `api.github.com`, `CUDA`, `INT8`, `fp32`, `E4B`, `E2B`, `mmproj`, `ADR-032`.

**Borrowed as loanwords, kept in Latin script inside Greek text** (Greek Windows and
everyday Greek tech usage do this rather than reaching for the native word, which would
read as stilted or academic here): `Cloud` (the widget badge stays exactly `Cloud`, and
`cloud` is used lowercase as an attributive loanword elsewhere — `πάροχος cloud`,
`μηχανή cloud` — never `νέφος`, which reads as an academic/telecom term, not a
software-UI one) and `backend` (`Settings_Runtime_Description`).

Windows/macOS feature names use the real localized wording those OSes use in Greek, not
literal translations:
- Task Manager → **Διαχείριση εργασιών**
- Startup apps tab → **Εφαρμογές εκκίνησης**
- File Explorer → **Εξερεύνηση αρχείων**
- Run dialog → **Εκτέλεση**
- Task View → **Προβολή εργασιών**
- Windows Settings app → **Ρυθμίσεις**
- Windows Speech Recognition → **Αναγνώριση ομιλίας των Windows**

Three judgment calls, flagged for a native check (same spirit as Ukrainian's flag on
the Win+X menu name):
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P) — rendered as "Προβολή σε
  οθόνη"; no single fixed Greek Windows term verified from memory.
- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — rendered as "Μενού γρήγορων
  συνδέσμων", a literal reading rather than a verified official term.
- **`Settings_Hotkeys_Reserved_SwitchInputSource`** — rendered as "Εναλλαγή γλώσσας
  εισόδου"; Greek Windows may phrase this slightly differently in the language bar.
- `Game Bar` is kept as the Microsoft brand name, not translated (`Άνοιγμα Game Bar`),
  matching how Greek Windows UI itself leaves this one untranslated.

## Core glossary

| English term | Greek | Notes |
|---|---|---|
| dictation | **υπαγόρευση** | App's core verb throughout; verb form "υπαγορεύω" where needed. |
| transcribe / transcription | **μεταγραφή** / μεταγράφω | "recognized text" → "αναγνωρισμένο κείμενο". Kept distinct from μετάφραση. |
| translate / translation | **μετάφραση** / μεταφράζω | Never used for transcribe. |
| recording | **εγγραφή** | Also the "Recording..." status → "Εγγραφή...". |
| speech engine | **μηχανή ομιλίας** (short: **μηχανή**) | `Settings_Category_SpeechEngine`/`Settings_Engine_Heading` use the full form; `Settings_Engine_Title` (nav label) uses the short form. |
| model | **μοντέλο** | |
| runtime | **περιβάλλον εκτέλεσης** | Matches the established Greek IT term (cf. "Java Runtime Environment" → "Περιβάλλον εκτέλεσης Java"). Longer than the English, but this is the term Greek technical writing actually uses. |
| source language | **γλώσσα πηγής** | Reused for "spoken language" wherever that phrase recurs — one term, one concept. |
| target language | **γλώσσα προορισμού** | The language Parlotype types. `Language_Target_TranslationHint` ("Translation target") also maps to this one term. |
| hotkey | **συντόμευση** (full: συντόμευση πληκτρολογίου) | Standard Greek software term; short form used throughout for room. |
| push-to-talk | **κράτημα για ομιλία** (badge/mid-sentence noun: **κράτημα**) | |
| toggle | **εναλλαγή** | Mode badge and mid-sentence noun. |
| binding | *(not a separate surfaced term)* | No key needs a standalone word distinct from "συντόμευση". |
| tray | **περιοχή ειδοποιήσεων** | Standard Greek Windows term for the notification area. |
| widget | **γραφικό στοιχείο** | Matches Windows 11's own Greek localization of "Widgets" → "Γραφικά στοιχεία". |
| waveform | *(no key in this set surfaces this string alone)* | Not present as standalone copy. |
| wait time | **χρόνος αναμονής** | `Settings_SilenceTimeout_Title` → "Χρόνος αναμονής"; the Medium/Long/Extended/Very Long options are masculine adjectives agreeing with "χρόνος" (Μεσαίος, Μεγάλος, Εκτεταμένος, Πολύ μεγάλος). |
| punctuation | **στίξη** | |
| profanity filter | **φίλτρο βωμολοχιών** | "βωμολοχία/-ες" used consistently for the masked words too. |
| inject / typed into | **πληκτρολογείται** (verb: πληκτρολογώ) | One verb for the whole concept — "Parlotype types" → "Το Parlotype πληκτρολογεί". No separate technical term invented for *inject*. |
| clipboard | **πρόχειρο** | Standard Greek Windows term. |
| cloud engine / cloud provider | **μηχανή cloud** / **πάροχος cloud** | `cloud` kept as a loanword — see "Never-translate register" above. The `Transcribe_CloudBadge` badge is simply **"Cloud"**. |
| API key | **κλειδί API** | |
| onboarding | **περιήγηση** | "Guided tour" — `Onboarding_WindowTitle` → "Περιήγηση Parlotype", `Onboarding_Nav_Skip` → "Παράλειψη περιήγησης". |
| restart required | **απαιτείται επανεκκίνηση** | |
| prompt (AI instruction, Gemma 4) | **προτροπή** | Standard Greek term for an AI/LLM prompt, distinct from "υπόδειξη" (hint/tooltip). |
| placeholder (prompt token) | **μεταβλητή** | `{speech_lang}`/`{text_lang}` are template variables substituted by name — "μεταβλητή" reads more naturally in Greek than a literal "placeholder" calque and is unambiguous next to the literal-token copy that surrounds it. |

## Case/gender-inflection flags (per brief.md's placeholder rule)

Greek marks four cases and three genders, and every gendered adjective or noun in a
predicate position must agree with the gender of its subject. A `{0}` substituted with a
raw, un-translated value (a model name, an engine name, a language name) arrives with no
case ending and no known gender, so any construction that would force a Greek adjective
to agree with it, or force the value itself to take a case ending, cannot be built
literally. Four keys needed restructuring for this reason:

- **`Language_ToggleSwitch_TranslateToFormat`** — `"Translate to {0}"`. Literal Greek
  says "Μετάφραση στα {0}" (language names in Greek are neuter-plural nouns taking a
  fixed accusative-with-article form, e.g. "στα Ισπανικά"), which the raw placeholder
  cannot produce since it never arrives pre-inflected as a Greek neuter plural.
  Restructured to **`"Μετάφραση: {0}"`** (label + colon), sidestepping the construction
  entirely. This is the same key — and the same fix — that French, Ukrainian, Polish and
  Finnish converged on independently (see the localization skill's "the part that
  bites" section).
- **`Language_Summary_Format`** — `"You speak {0} → Parlotype types {1}."`. The same
  convergent key. Idiomatic Greek would say "Μιλάτε Ισπανικά" (an accusative-form
  language name after the verb), which the raw placeholder can't supply reliably.
  Restructured to **`"Ομιλία: {0} → Το Parlotype πληκτρολογεί: {1}."`**, putting both
  slots after a colon so neither needs a grammatical case.
- **`Language_Toast_TargetUnsupportedFormat`** — `"{0} isn't available in {1}.
  Translation set to {2}."`. The third convergent key. A literal adjective rendering
  ("{0} δεν είναι διαθέσιμη/-ος/-ο σε {1}") would force gender agreement on "διαθέσιμη"
  with a language name whose grammatical gender the raw string can't tell us. Rewritten
  with an invariant verb instead of a gendered adjective: **`"Το {0} δεν υποστηρίζεται
  στο {1}. Γλώσσα μετάφρασης: {2}."`** — Greek 3rd-person verbs don't inflect for
  gender, only adjectives and some nouns do, so "υποστηρίζεται" is safe regardless of
  {0}'s underlying gender, and {2} moved behind a colon for the same reason.
- **`Cloud_Error_ProviderUnavailableFormat`** — `"{0} is unavailable right now (HTTP
  {1})..."`. Same gender-agreement risk: {0} is `Cloud_ProviderName_OpenAiCompatible`
  (a phrase headed by the masculine noun "πάροχος") or `Cloud_ProviderName_XaiGrok` (an
  indeclinable foreign name with no clear gender), and a literal "μη διαθέσιμος/-η/-ο"
  would need to agree with whichever it turns out to be. Rewritten with a verb instead
  of an adjective: **`"Το {0} δεν αποκρίνεται αυτή τη στιγμή (HTTP {1})..."`** ("isn't
  responding"), which is invariant for the same reason as above. This follows through on
  the design the brief's own `Cloud_ProviderName_*` comment calls for — the name stays
  nominative, followed by a verb, so no language needs to inflect it.

No other key in the 389 needed rephrasing for this reason. Keys like
`Settings_Hotkeys_Modifier_LeftFormat`/`RightFormat` ("Αριστερό {0}"/"Δεξί {0}") do put
a gendered adjective next to {0}, but the adjective agrees with the *implied* noun
"πλήκτρο" (key, neuter) by fixed convention, not with {0} itself — {0} is always a
foreign, indeclinable key name (Ctrl, Alt, Shift), so the neuter form is always correct
regardless of what fills the slot, and no rephrasing was needed there.

## Things I was unsure about

- The three Windows/macOS feature-name renderings flagged above
  (`Settings_Hotkeys_Reserved_ProjectDisplay`, `QuickLinkMenu`, `SwitchInputSource`) —
  best-effort literal renderings, not verified against current Microsoft Greek
  localization strings.
- **`Settings_Hotkeys_Reserved_WindowsSpeechRecognition`** — rendered as "Αναγνώριση
  ομιλίας των Windows"; plausible but not verified against the exact current Windows
  Greek Ease of Access naming.
- **`περιβάλλον εκτέλεσης`** for "runtime" runs noticeably longer than the English on
  `Settings_WhisperRuntime_Title` ("Whisper runtime" → "Περιβάλλον εκτέλεσης Whisper",
  roughly 2× the character count). I kept it because it is the term Greek technical
  writing actually uses and no shorter alternative reads as established Greek software
  vocabulary; flagging in case the nav column turns out too narrow for it in practice.
- **`μεταβλητή`** for "placeholder" (prompt tokens) — chosen over a literal calque of
  "placeholder" for naturalness; a native reviewer may prefer a different established
  term if one exists in Greek AI-tooling contexts by the time this ships.

## Validation performed

Every key was checked against its brief file: all 389 keys present across the three
parts with no extras and no key duplicated between files, every `{0}`/`{1}`/`{2}`
placeholder set matches the English source exactly (same count, same indices, none
added or dropped), the literal template tokens `{speech_lang}` and `{text_lang}` inside
`Settings_Prompts_Help_BuiltInBody` are preserved verbatim and untranslated, and no
value over 25 characters is byte-identical to its English source. Monotonic
accentuation was applied to every polysyllabic Greek word by hand during translation;
this was not machine-checked and is the area most worth a native-speaker pass.
