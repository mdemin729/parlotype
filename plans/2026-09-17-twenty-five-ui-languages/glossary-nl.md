---
title: Dutch (nl) translation glossary
status: reference
created: 2026-09-17
---

# Dutch (nl) glossary

Companion to `brief.md`. Choices made for the 389-key Dutch translation in
`<scratch>/nl.json`.

## Register

**Je** (informal), never **u**. Chosen for the same reason German chose **du** over
**Sie**: Parlotype is a small personal utility, not enterprise software, and modern
Dutch consumer software (Spotify, WhatsApp, most indie/utility apps) defaults to "je".
Windows itself still uses "u" in system dialogs, but Parlotype isn't impersonating a
Windows dialog — when a sentence names an actual Windows feature (Taakbeheer,
Verkenner, Opstartapps), the *feature name* is Windows' own wording; the *sentence
around it* stays in Parlotype's "je" voice. Imperatives drop to the bare stem
("Kies", "Voeg toe", "Klik", "Sleep") the way "je"-register Dutch always does — no
"-t" suffix, no "u" form anywhere. Possessives are "je/jouw" throughout, never
"uw". Never mixed with "u"-forms.

"Je" is also the shorter option in the handful of places where it matters
(no "-t" verb suffix), though Dutch does not carry German's severe compounding-length
problem, so this was a secondary consideration, not the deciding one.

## Quotation convention

- **Double curly quotes** `“…”` (opening/closing curved the same direction as
  English, U+201C/U+201D) for quoted terms and literal fragments, e.g. `“Cloud”`,
  `“{0}”`. This is the primary quotation style in current Dutch typography (Onze
  Taal's own house style uses this shape), and it reads unambiguously in a UI font
  where a low-9 opening quote can be mistaken for a comma at small sizes.
- **Em dash** `—` (with plain spaces) for parenthetical/status clauses, matching the
  English source's own `—` directly: `Gereed — Opname bezig`,
  `Cloud-aanbieder niet beschikbaar — probeer het zo weer`. Unlike German, Dutch
  typography does not conventionally shorten this to an en dash, so the source
  em dash is kept as-is throughout.
- **Ellipsis** `…` (single U+2026 character) for in-progress states —
  `Model laden…`, `Opname bezig…` — attached directly to the preceding word with no
  space, which is the more common convention in Dutch UI copy (mirrors how Windows'
  own Dutch localization writes "Bezig met laden…").
- **Non-breaking space (U+00A0)** between a number and the kept unit token in
  `Settings_SilenceTimeout_WaitFormat` (`{1} s`), matching Dutch typography's
  rule against letting a number and its unit break across a line. Sizes like
  "670 MB" / "~10 GB" inside running prose use a plain space, following the same
  precedent the German and Spanish files already set — only the dedicated numeric
  format gets the nbsp treatment.

## Never-translate register

Followed exactly as specified in `brief.md`: `Parlotype`, `Whisper`, `Parakeet TDT v3`,
`Parakeet`, `Gemma 4`, `sherpa-onnx`, `llama.cpp`, `llama-server`, `Silero VAD`,
`Vulkan`, `ONNX`, `OpenAI`, `Groq`, `xAI`, `Grok`, `gpt-4o-mini-transcribe`,
`whisper-large-v3`, `Large v3 Turbo`, `settings.json`, `secrets.json`,
`%LOCALAPPDATA%`, `WAV`, `GB`, `MB`, `ms`, `Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc`/
`Tab`/`Enter`, `Setup.exe`, `vulkan-1.dll`, `HKCU Run`, `api.github.com`,
`llama-server.exe`. `Settings_Hotkeys_Conflict_AltGrFormat` keeps "Space" verbatim in
the Ctrl+Alt+Space suggestion — easy to miss since it reads like ordinary prose, but
it is the literal key name.

Loanwords kept deliberately, as naturalized Dutch tech vocabulary rather than
translated concepts:
- **Widget** — stays "widget"; Dutch (including Microsoft's own Windows 11 "Widgets"
  taskbar feature) uses this word unchanged, and there is no shorter/more natural
  native alternative.
- **Cloud** — stays "cloud" throughout compounds ("cloud-engine", "cloud-aanbieder").
  It is now the dominant Dutch tech term; "wolk" would read as a mistranslation.
- **Runtime** — kept as "runtime" (not translated to e.g. "uitvoeringsomgeving",
  which is both longer and unfamiliar). Established Dutch developer vocabulary
  already says "runtime" (".NET-runtime", "runtime-fout").
- **Prompt(s)**, **Build(s)**, **Update(s)** — kept as English loanwords; these are
  the terms a Dutch-speaking user of a speech-tech settings page already knows, and
  translating them ("melding", "versie", "bijwerking") would read as unfamiliar
  and add no clarity.
- **Push-to-talk** — kept as the fixed English term, exactly as Discord, Windows and
  virtually all Dutch voice-chat/dictation software use it verbatim. No Dutch fixed
  equivalent exists.
- **Placeholders** — kept as the English developer term rather than "plaatshouders",
  since this string is aimed at a user editing prompt text and "Placeholders" is the
  term they will already recognize from any templating tool.

Windows feature names use their real Dutch Windows UI wording, not literal
translations, per brief.md's "prefer the verb form your OS uses":
- Task Manager → **Taakbeheer**
- Startup apps tab → **Opstartapps**
- File Explorer → **Verkenner**
- Run dialog → **Uitvoeren**
- Task View → **Taakweergave**
- Win+P pane → **Projecteren**
- Win+X menu → **Snelkoppelingenmenu**
- Game Bar → **Xbox Game Bar**
- Win+H (Voice Typing) → **Windows-spraaktoetsen**
- Win+L (lock) → **Vergrendelen**

## Core glossary

| English term | Dutch | Notes |
|---|---|---|
| dictation | **dicteren** (verb) / **dictee-** (compound prefix) | `Onboarding_Recording_Title` → "Begin met dicteren"; compounds as "dictee-sneltoets(en)" for dictation hotkeys throughout, matching how Dutch already uses "dictee" as a noun for a spoken-to-written exercise. |
| transcribe | **transcriberen** / **transcriptie** | Kept distinct from *vertalen*. |
| translate | **vertalen** / **vertaling** | Never used to mean *transcribe*. |
| recording | **opname** | Also the "Recording..." status → "Opname bezig…". |
| speech engine | **spraakengine** | One compound noun throughout (`Settings_Category_SpeechEngine`, `Settings_Engine_Heading`). The narrow nav label `Settings_Engine_Title` uses bare **"Engine"**, since the category above it already supplies "Spraak-" — same split German made for the identical reason. |
| engine (bare) | **engine** | Kept as an established Dutch tech loanword ("game-engine", "zoekengine") rather than an invented term like "herkenningsmodule". |
| model | **model** (het) | |
| runtime | **runtime** (de) | Kept as a loanword — see "Never-translate register" above. |
| source language | **brontaal** | |
| target language | **doeltaal** | |
| hotkey | **sneltoets** | Matches the word Dutch Windows itself uses for keyboard shortcuts. |
| push-to-talk | **Push-to-talk** | Kept as the fixed English term — see above. |
| toggle | **schakelen** | Verb and mode label both use "schakelen"/"Schakelen" consistently, including the "switch between modes" tooltip ("Wisselen tussen push-to-talk en schakelen"). |
| binding | *(not a separate surfaced term)* | No dedicated `_Binding` key exists in this key set; always expressed via "sneltoets". |
| tray | **systeemvak** | The actual Windows Dutch name for the notification area (not "taakbalk", the whole taskbar). |
| widget | **widget** (de) | Kept, see "Never-translate register" above. |
| waveform | *(no key in this set surfaces this string alone)* | Not directly present as standalone copy in the 389 keys. |
| wait time | **wachttijd** | `Settings_SilenceTimeout_Title` → "Wachttijd"; used consistently for "how long the app waits through silence." |
| punctuation | **leestekens** | Chosen over the more formal "interpunctie" — shorter, and it's the word Dutch speakers actually use for commas/periods in running text. |
| profanity filter | **vloekfilter** | "Scheldwoorden" used consistently for the masked words themselves. |
| inject / typed into | **typen** | "de herkende tekst wordt … getypt" — mirrors the English "typed" directly and stays natural and short in both body copy and short captions ("Parlotype typt"). |
| clipboard | **klembord** | Standard Dutch Windows term. |
| cloud engine / cloud provider | **cloud-engine** / **cloud-aanbieder** | "Cloud" kept as a loanword (see above); "aanbieder" for *provider* throughout, never "provider" itself, even though that is also a valid Dutch loanword — "aanbieder" is the word Dutch privacy/ToS copy already uses for a service provider, and it distinguishes cleanly from "provider" meaning an ISP. |
| API key | **API-sleutel** (de) | |
| onboarding | **tour** (de) | Matches the English source's own choice of word (`Onboarding_WindowTitle` = "Parlotype tour", not "Parlotype onboarding") — Dutch keeps "tour" as a short, fully naturalized loanword. |
| restart required | **herstart vereist** | |

## de/het decisions

Dutch borrowed nouns need a fixed gender/article the moment they take a modifier
("deze cloud-engine" vs. "dit cloud-engine"). Decided once and kept consistent:

| Word | Article | Reasoning |
|---|---|---|
| widget | **de** widget | Common gender, as with most anglicisms for small devices/gadgets ("de app", "de tool"). |
| engine | **de** engine | Common gender, by analogy with "de motor". |
| model | **het** model | Standard, pre-existing Dutch word — already neuter regardless of this project. |
| prompt | **de** prompt | Common gender, by analogy with "de tekst"/"de instructie". |
| cloud (bare noun use) | **de** cloud | Common gender, as in "de cloud-opslag"; note the bare noun never actually appears alone as a subject in this key set — it only appears as a compound prefix ("cloud-engine", "cloud-aanbieder") or the untranslated badge "Cloud", so this decision is precautionary rather than load-bearing. |
| API-sleutel | **de** API-sleutel | Inherits gender from "sleutel", which is common gender. |
| tour | **de** tour | Common gender, as in "de rondleiding". |
| runtime | **de** runtime | Common gender loanword, by analogy with "de uitvoering". |

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_SecurityScreen`** (Ctrl+Alt+Del) — used
  "Beveiligingsscherm". Windows' own Dutch wording for this varies by version and
  context (the screen itself is sometimes titled "Windows-beveiligingsopties"); worth
  checking against the current Windows 11 Dutch build.
- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** (Win+X) — used
  "Snelkoppelingenmenu". There isn't one single official Dutch Microsoft name used
  consistently across support docs and Windows versions for this menu; a native
  speaker with the current build should confirm.
- **`Settings_Hotkeys_Reserved_LockWorkstation`** (Win+L) — used the bare
  "Vergrendelen", matching the label on the actual Ctrl+Alt+Del/Start-menu lock
  action in Dutch Windows, rather than a literal "werkstation vergrendelen" which
  isn't wording Windows itself uses.
- **`ModelDownload_ConfirmWithProjectorFormat`** ("includes vision projector") — kept
  "vision projector" untranslated as a technical ML term (parallel to "mmproj" itself
  being an untranslated identifier). No established Dutch equivalent exists in this
  domain, and inventing one ("visie-projector") would be both unfamiliar and
  misleading about what the component does.
- **`Settings_Runtime_Cpu_Description`** ("Force CPU-only inference") — translated
  "inference" as **verwerking** ("Forceert verwerking op alleen de CPU") rather than
  keeping the ML-jargon "inference" or the stiffer "gevolgtrekking". This is a
  judgment call for a general settings audience rather than a forced necessity —
  flagging it because a more ML-literate audience might prefer "inferentie" kept as
  a loanword instead.
- **`Settings_LlamaCpp_NoManagedInstalls`** — the English says "the Available list
  below" but the heading it points at (`Settings_LlamaCpp_AvailableBuildsHeading`) is
  actually "Available **builds**". I matched the heading's exact wording
  ("Beschikbare builds") rather than a literal "Beschikbaar", per the brief's own
  instruction that this cross-reference must name that list precisely.

## Placeholder / grammatical-case note

Like German and unlike Russian, **Dutch does not inflect nouns for grammatical
case** — no case endings on substituted proper nouns (engine names, language names,
version numbers, chords). Because every `{0}`/`{1}`/`{2}` slot in this key set holds
such a proper noun rather than a common noun needing an article, **no key forced a
rephrasing of the English** for grammatical reasons. I still kept substituted values
at clause edges (after a colon, dash or full stop) wherever the English already did
so, since it costs nothing and keeps the Dutch easy to scan — e.g.
`Cloud_NotConfigured_MissingKeyFormat` puts `{0}` before a colon rather than inside a
prepositional phrase.

The one Dutch-specific agreement point is `Settings_Hotkeys_Modifier_LeftFormat` /
`RightFormat` (`"Left {0}"` / `"Right {0}"` → `"Linker {0}"` / `"Rechter {0}"`): Dutch
"linker"/"rechter" are invariant attributive forms used exactly this way before a
bare noun ("linkerhand", "rechter Ctrl-toets"), so no article or agreement decision
was needed — this one resolved cleanly, unlike German's "linke/rechte" judgment
call.

## Validation performed

Every key was checked programmatically against `nl.brief.json`: all 389 keys present
with no extras or omissions, every `{0}`/`{1}`/`{2}` placeholder set matches the
English source exactly, the literal template tokens `{speech_lang}` and `{text_lang}`
inside `Settings_Prompts_Help_BuiltInBody` are preserved verbatim, and no value over
25 characters matches the English source byte-for-byte.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Standaard (woordelijke transcriptie)"** — verbatim/word-for-word rendered as *woordelijke (word-for-word)*, paired with the already-established *transcriptie* for transcription; no new terms introduced.


## Hotkey gesture grammar (correction 2026-10-03)

`Settings_Hotkeys_Modifier_LeftFormat` / `RightFormat` are substituted into `Gesture_HoldFormat` / `Gesture_DoubleTapFormat`, so the side is a *mid-phrase* word: lowercase, in the case the gesture verb governs. Rendered result for the default hotkey: **`Houd rechter Ctrl ingedrukt`.**

Side is now `rechter {0}` / `linker {0}`, lowercase. `Conflict_AlreadyBoundFormat` quotes the imperative gesture: `“{0}” is al gekoppeld aan {1}.`
