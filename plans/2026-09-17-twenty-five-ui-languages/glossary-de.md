---
title: German (de) translation glossary
status: reference
created: 2026-09-17
---

# German (de) glossary

Companion to `brief.md`. Choices made for the 389-key German translation in
`<scratch>/de.json`.

## Register

**Du** (informal), never **Sie**. Chosen over the more traditional enterprise-software
"Sie" for two reasons: (1) it is now the norm for consumer desktop/mobile software in
German — Spotify, Duolingo, Telegram, and most indie/utility apps address the user as
"du" — and Parlotype is a small personal utility, not enterprise software; (2) it is
measurably shorter, which matters given the brief's warning that German is the worst
case for text expansion in this app. The "du" imperative drops the pronoun entirely
("Drücke", "Wähle", "Starte") where the "Sie" form would need "Drücken Sie",
"Wählen Sie" — a real, repeated cost across ~40 imperative strings in this file.
Used consistently across onboarding, tooltips, error text and settings descriptions.
Possessives are "dein/deine/deinen" throughout, never "Ihr/Ihre". Never mixed with
Sie-forms.

Windows' own German UI mostly uses "Sie" in system dialogs, but Parlotype is not
impersonating a Windows system dialog — where Windows feature names themselves are
referenced (Task-Manager, Autostart, Sperren), the *names* are Windows' own wording;
the *sentences around them* stay in Parlotype's "du" voice.

## Quotation convention

- **Anführungszeichen** `„…“` (opening low-9, closing high-6) for quoted terms and
  literal fragments, e.g. `„Cloud“`, `„{0}“`. This is standard German typography, as
  distinct from French/Spanish `«…»` or English `"…"`. No extra spacing inside the
  marks (unlike French).
- **En dash** `–` (with plain spaces) for parenthetical/status clauses, mirroring the
  English source's `—`/`-` separated clauses: `Bereit – Aufnahme läuft`,
  `Cloud-Anbieter nicht erreichbar – gleich erneut versuchen`. German typography
  conventionally uses the shorter en dash `–` where English uses an em dash `—`.
  Also used for "hold to X" style Esc-lines: `Esc – bricht die Aufnahme ab`.
- **Non-breaking space (U+00A0)** between a number and a kept unit token
  (`{1} s` in `Settings_SilenceTimeout_WaitFormat`), matching German
  typography's rule of never letting a number and its unit break across lines.
  Sizes like `670 MB` / `10 GB` inside prose sentences use a plain space, following
  the precedent already set in `Strings.es.resx` and `Strings.ru.resx`, which do not
  insert an nbsp into running prose either — only the dedicated numeric format got
  the nbsp treatment here.
- **Ellipsis** `…` (single U+2026 character, preceded by a space) for in-progress
  states — `Modell wird geladen …`, `Aufnahme läuft …` — rather than three literal
  periods, following standard German typesetting practice.

## Never-translate register

Followed exactly as specified in `brief.md`: `Parlotype`, `Whisper`, `Parakeet TDT v3`,
`Parakeet`, `Gemma 4`, `sherpa-onnx`, `llama.cpp`, `llama-server`, `Silero VAD`,
`Vulkan`, `ONNX`, `OpenAI`, `Groq`, `xAI`, `Grok`, `gpt-4o-mini-transcribe`,
`whisper-large-v3`, `Large v3 Turbo`, `settings.json`, `secrets.json`,
`%LOCALAPPDATA%`, `WAV`, `GB`, `MB`, `ms`, `Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc`/
`Tab`/`Enter`, `Setup.exe`, `vulkan-1.dll`, `HKCU Run`, `api.github.com`,
`llama-server.exe`.

Two loanwords kept deliberately, as naturalized German tech vocabulary rather than
translated concepts:
- **Widget** — stays "Widget"; there is no shorter native German word for this kind
  of floating always-on-top panel, and German software (including Microsoft's own)
  uses "Widget" as-is (cf. the Windows 11 "Widgets" taskbar feature).
- **Cloud** — stays "Cloud" (`Transcribe_CloudBadge` = "Cloud", and "Cloud-Engine" /
  "Cloud-Anbieter" as compounds). "Cloud" is now the dominant German tech term;
  translating it to "Datenwolke" would read as stilted and unrecognizable.
- **Prompts**, **Build**, **Updates** — kept as English loanwords; these are the terms
  a German-speaking developer-adjacent user of a speech-tech settings page already
  knows, and back-translating them ("Eingabeaufforderungen", "Erstellung",
  "Aktualisierungen") would be both longer and less recognizable.

`Windows` feature names are rendered with their real German Windows UI wording, not
literal translations, per brief.md's "prefer the verb form your OS uses":
- Task Manager → **Task-Manager**
- Startup apps tab → **Autostart**
- File Explorer → **Datei-Explorer**
- Run dialog → **Ausführen**
- Task View → **Taskansicht**
- Win+P pane → **Projizieren**
- Win+X menu → **Schnellzugriffsmenü**
- Game Bar → **Xbox Game Bar**
- Win+H → **Windows-Spracheingabe**
- Win+L → **Computer sperren** (not a literal "Arbeitsstation sperren" — this is the
  actual tooltip/label Windows itself uses)

## Core glossary

| English term | German | Notes |
|---|---|---|
| dictation | **Diktat** | `Onboarding_Recording_Title` → "Diktat starten"; compounds as "Diktat-Tastenkürzel" for dictation hotkeys throughout. |
| transcribe | **transkribieren** / **Transkription** | Kept distinct from *übersetzen*. |
| translate | **übersetzen** / **Übersetzung** | Never used to mean *transcribe*. |
| recording | **Aufnahme** | Also the "Recording..." status → "Aufnahme läuft …". |
| speech engine | **Sprachengine** | One compound noun throughout — short enough to survive tight nav rows (`Settings_Category_SpeechEngine`) and page headings (`Settings_Engine_Heading`) alike. The short nav label `Settings_Engine_Title` uses bare **"Engine"** (kept as a loanword — see below), since the category above it already supplies "Sprach-". |
| engine (bare) | **Engine** | Kept as an established German tech loanword (as in "Rendering-Engine", "Spiele-Engine") rather than invented terms like "Erkennungsmodul", which would be both longer and less familiar. |
| model | **Modell** | |
| runtime | **Laufzeit** | Chosen over keeping "Runtime" as a loanword because "Laufzeit" is genuinely idiomatic, established German computing vocabulary (".NET-Laufzeit", "zur Laufzeit") and is no longer than the English. |
| source language | **Ausgangssprache** | |
| target language | **Zielsprache** | |
| hotkey | **Tastenkürzel** | Single term throughout; matches the word Windows' own German UI uses for keyboard shortcuts. |
| push-to-talk | **Push-to-Talk** | Kept as the English term — this is the term used verbatim by Discord, TeamSpeak, Windows and virtually all German voice-chat/dictation software; a translated alternative ("Sprechtaste gedrückt halten") doesn't exist as a fixed term. |
| toggle | **Umschalten** | Verb "umschalten" / noun-ish mode label "Umschalten", used consistently for both the mode name and the "switch between modes" tooltip. |
| binding | *(not a separate surfaced term)* | No dedicated `_Binding` key exists in this key set; always expressed via "Tastenkürzel". |
| tray | **Infobereich** | The actual Windows German name for the system notification area (not "Taskleiste", which is the whole taskbar, nor "Systray"). |
| widget | **Widget** | Kept, see "Never-translate register" above. |
| waveform | *(no key in this set surfaces this string alone)* | Not directly present as standalone copy in the 389 keys. |
| wait time | **Wartezeit** | `Settings_SilenceTimeout_Title` → "Wartezeit"; used consistently for "how long the app waits through silence." |
| punctuation | **Zeichensetzung** | |
| profanity filter | **Schimpfwortfilter** | "Schimpfwörter" used consistently for the masked words themselves too. |
| inject / typed into | **tippen** | "der erkannte Text wird … getippt" — chosen over the more formal "eingeben" for brevity and because it directly mirrors the literal English "typed", staying natural and short in both body copy and the short caption `Parlotype tippt`. |
| clipboard | **Zwischenablage** | Standard Windows German term; surfaces in `Settings_Data_PathCopied`-adjacent copy. |
| cloud engine / cloud provider | **Cloud-Engine** / **Cloud-Anbieter** | "Cloud" kept as a loanword (see above); "Anbieter" for *provider* throughout, never "Provider" (also a valid loanword, but "Anbieter" is shorter and equally natural). |
| API key | **API-Schlüssel** | |
| onboarding | **Tour** | Matches the English source's own choice of word (`Onboarding_WindowTitle` = "Parlotype tour", not "Parlotype onboarding") — German keeps "Tour" as a short, natural loanword rather than "geführte Einführung", which would be longer. |
| restart required | **Neustart erforderlich** | |

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — there is no single fixed
  official German name for this menu across Windows versions; I used
  "Schnellzugriffsmenü", which matches common German Windows documentation, but a
  native speaker with the exact current Windows 11 German build should confirm it
  against Microsoft's own support docs.
- **`Settings_Hotkeys_Reserved_SecurityScreen`** (Ctrl+Alt+Del) — I used
  "Sicherheitsbildschirm"; Windows' own wording varies by version
  ("Sicherheitsoptionen" is also used). Worth checking against the current build.
- **"Engine" vs. "Sprachengine"** — I split these deliberately (bare "Engine" only in
  the tightest nav-row context, `Settings_Engine_Title`) rather than using one term
  everywhere, because "Sprachengine" alone in that narrow row risked wrapping. This
  is a readability trade-off the maintainer may want to revisit if it reads as
  inconsistent rather than intentional.
- **`Settings_Hotkeys_Conflict_AlreadyBoundFormat`** (`"{0} is already bound to {1}."`)
  — I restructured this from a literal "ist an {1} gebunden" to "ist bereits
  vergeben: {1}." for brevity and to keep the substituted mode name safely after a
  colon. This isn't forced by German grammar (see placeholder note below) — it's a
  style choice for terseness — but I'm flagging the restructure since it changes the
  sentence shape from the English original.
- **`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat`** (`"Left {0}"` /
  `"Right {0}"` → `"Linke {0}"` / `"Rechte {0}"`) — the adjective ending `-e` assumes
  an implicit feminine "Taste" ("linke [Taste]"). Since `{0}` is always an
  untranslated key name (`Ctrl`, `Alt`, `Shift`), there's no real noun for the
  adjective to agree with; I used the feminine form uniformly since that's how a
  German speaker would silently complete "linke ___" when the word "Taste" is
  implied. Flagging as a judgment call rather than a forced necessity — see the
  placeholder note below.

## Placeholder / grammatical-case note

Unlike Russian, **German does not inflect most nouns for grammatical case** — only
the genitive adds `-s`/`-es`, and bare proper nouns (product names, language names,
key names) substituted without a preceding definite article are not case-marked at
all in normal usage. Because every `{0}`/`{1}`/`{2}` slot in this key set holds a
proper noun (an engine name, a language name, a chord, a version number) rather than
a common noun needing an article, **no key in the 389 forced a grammatical-case
rephrasing of the English** — this is a genuine difference from Russian, not an
oversight. I still followed the brief's defensive practice of keeping substituted
values at clause edges (after a colon or dash) wherever the English already did so,
since it costs nothing and keeps the German easy to scan.

The one place gender (not case) shows up is `Settings_Hotkeys_Modifier_LeftFormat` /
`RightFormat`, noted above — an adjective-agreement judgment call, not a blocked
translation.

## Validation performed

Every key was checked programmatically against `de.brief.json`: all 389 keys present
with no extras, every `{0}`/`{1}`/`{2}` placeholder set matches the English source
exactly, the literal template tokens `{speech_lang}` and `{text_lang}` used inside
prompt-help copy are preserved verbatim, and no value over 25 characters matches the
English source byte-for-byte.
