---
title: Italian glossary — core terms, register and conventions
status: reference
created: 2026-09-17
---

# Italian (it) glossary

Companion to `it.brief.json` / `it.json`. 389 keys translated.

## Formality register

**Informal "tu"**, matching modern Microsoft/Google/Apple Italian consumer-software
convention (Windows 10/11 Settings, for example, uses "tu": *"Personalizza il tuo PC"*).
Imperatives are therefore the *tu* form throughout ("Scegli", "Aggiungi", "Tieni premuto"),
never the *Lei* form ("Scelga", "Aggiunga"). Never mixed.

## Quotation convention

**Guillemets `«…»`**, the standard Italian typographic quotation mark, used for every
quoted phrase and every prompt/placeholder example (`«{0}»`, `«poi traduci in ...»`).
This matches the existing Spanish file's choice of `«…»` over straight quotes. No nested
quoting was needed anywhere in this set.

## Units and spacing

A plain space before a unit (`{1} s`), matching the existing Spanish resx precedent — I
found no evidence Italian typography requires a non-breaking space here, unlike French.

## Core glossary

| Term | Italian | Notes |
|---|---|---|
| dictation | **dettatura** | |
| transcribe / transcription | **trascrivere** / **trascrizione** | |
| translate / translation | **tradurre** / **traduzione** | Kept strictly distinct from trascrivere per the brief. |
| recording | **registrazione** | |
| speech engine | **motore vocale** (short: **motore**) | Category label uses the full form; the sub-item nav label uses "Motore" alone, mirroring the English Title/Heading split. |
| model | **modello** | |
| runtime | **runtime** (borrowed, masculine — *il runtime*) | |
| source language | **lingua di partenza** | Matches common Italian localization convention (e.g. Google Translate IT). |
| target language | **lingua di destinazione** | |
| hotkey | **scorciatoia** (fem.), plural **scorciatoie** | See "Hotkey — a deliberate deviation" below. |
| push-to-talk | **Tieni premuto** (badge); **tieni premuto** mid-sentence | Matches Discord's Italian localization of the identical feature. |
| toggle (activation mode) | **Attiva/disattiva** (badge); **attiva/disattiva** mid-sentence | |
| binding | *(not surfaced as its own translated string in this file)* | |
| tray | **area di notifica** | Official Windows Italian term for the system tray. |
| widget | **widget** (borrowed, masculine — *il widget*) | |
| waveform | **forma d'onda** | |
| wait time | **tempo di attesa** | |
| punctuation | **punteggiatura** | |
| profanity filter | **filtro turpiloquio** | *turpiloquio* is the standard Italian noun for profanity. |
| inject / typed into | **digitato in** | Natural phrasing, no invented technical term, per the brief. |
| clipboard | **Appunti** (capitalized, as Windows Italian does — *gli Appunti*) | |
| cloud engine / provider | **motore cloud** / **provider cloud** | "Cloud" itself is kept as the badge text (see below). |
| API key | **chiave API** (fem. — *la chiave API*) | |
| onboarding | **tour** (borrowed, e.g. *"Tour di Parlotype"*) | |
| restart required | **Riavvio necessario** | |

### "Cloud" badge — kept untranslated

`Transcribe_CloudBadge` ("Cloud") is kept as **"Cloud"** rather than translated to a native
noun. This is a deliberate difference from the Spanish file (which uses "Nube"): "cloud" as
a bare tech noun is extremely common, unmarked vocabulary in Italian IT contexts (Windows
and Microsoft 365's own Italian UI say "cloud", not "nuvola"), so keeping it reads as more
natural and idiomatic than translating it, not less.

### Hotkey — a deliberate deviation from a literal Windows term

Windows' own Italian term for a keyboard shortcut is **"tasto di scelta rapida"**
(plural *tasti di scelta rapida*), and my first pass used it throughout. However, with 389
compact strings — many of them buttons and tooltips in a small widget/settings window —
that phrase blew past the brief's "twice as long as English is a bug" rule on several keys
(e.g. `Settings_Hotkeys_AddButton`, "Add hotkey" → "Aggiungi tasto di scelta rapida" was
3.1× the English length). I switched the glossary term to **"scorciatoia"** (fem., plural
"scorciatoie") — shorter, and it is exactly the term Discord's own Italian localization
uses for this identical feature (a global push-to-talk key bind), so it is not an
invented shortcut. `Ctrl`/`Alt`/`Shift`/`Win`/`Space`/`Esc` and physical key presses
("tieni premuto un **tasto**") still correctly use *tasto* — only the higher-level
"hotkey" concept moved to *scorciatoia*.

## Windows/macOS feature names used verbatim from the OS

Per the brief's instruction to "use the wording the user's own operating system uses" for
reserved-shortcut descriptions:

- Task Manager → **Gestione attività**; its Startup tab → **Avvio**; its enable button →
  **Abilita** (all three used consistently across `Settings_Startup_*` and
  `Settings_Hotkeys_Reserved_*`).
- File Explorer → **Esplora file**; the Run dialog → **Esegui**; Task View →
  **Visualizzazione attività**; Windows Speech Recognition → **Riconoscimento vocale di
  Windows**; Windows' dictation feature (Win+H) → **Digitazione vocale**.
- Xbox Game Bar is kept as the product name: **Xbox Game Bar**.

## Never-translate register

Verified programmatically that every identifier from brief.md's register (Parlotype,
Whisper, Parakeet / Parakeet TDT v3, Gemma 4, sherpa-onnx, llama.cpp, llama-server, Silero
VAD, Vulkan, ONNX, OpenAI, Groq, xAI, Grok, `settings.json`, `secrets.json`,
`%LOCALAPPDATA%`, GB/MB/ms, `HKCU Run`, `Setup.exe`, `api.github.com`, `vulkan-1.dll`,
CUDA, mmproj, E4B/E2B, INT8, fp32, Medium/Large v1/v2/v3, and Ctrl/Alt/Shift/Space/Esc)
survived untouched in every string where the English contains it.

## Gender agreement and placeholders — no rephrasing needed for Italian

I checked every `{0}`/`{1}`/`{2}` slot for the class of problem the brief calls out for
Russian/German (a substituted value forcing a case ending or gendered agreement). Italian
does not mark grammatical case, and the two noun categories most often substituted here —
**language names** (always masculine in Italian: *l'italiano, il francese, lo spagnolo*)
and **engine/model/runtime names** used against fixed category nouns (*motore*, *modello*,
*runtime* — all consistently masculine regardless of which name fills the slot) — never
vary in gender depending on which value is substituted. So no key in this file needed the
English rephrased for an Italian-specific agreement problem.

The one placeholder pattern worth flagging for the maintainer, not because it broke
anything but because it was a close call: `Settings_Hotkeys_Modifier_LeftFormat` /
`RightFormat` ("Left {0}" / "Right {0}") render as **"{0} sinistro"** / **"{0} destro"** —
postposed, like Spanish's "izquierdo"/"derecho". This works only because the fixed set of
values that ever fills `{0}` here (Ctrl, Alt, Shift, Win) are all conventionally treated as
masculine loanwords in Italian; if a future key name were added that Italian speakers would
gender differently, this format would need to change.

## Things I was unsure about

- **`Settings_Hotkeys_Reserved_QuickLinkMenu`** ("Quick Link menu", the Win+X menu): I used
  "Menu di collegamento rapido". Microsoft's own Italian support content is inconsistent
  here (also seen as "menu Windows+X" or left as "Power User Menu"); there is no single
  authoritative Italian name the way there is for "Gestione attività" or "Esplora file".
- **`Settings_Hotkeys_Reserved_ProjectDisplay`** (Win+P): translated as "Proietta su
  schermo". The actual Windows Italian quick-panel title is just "Proietta", which felt too
  bare as a standalone list item next to full noun phrases like "Apri Esplora file".
- **`Settings_Hotkeys_Reserved_Screenshot`**: kept as the loanword "Screenshot" rather than
  "Cattura schermata" (the verb-phrase Windows actually uses for its snipping feature),
  favoring brevity for this compact list; a native speaker reviewing the reserved-shortcut
  list may prefer the latter.
- **`Settings_Prompts_Help_BuiltInBody`**: the embedded example "speech in X into X text"
  became «audio in X in testo in X» — a literal structural translation of an English
  placeholder-pattern example rather than natural prose, because it is explaining the
  *shape* of a built-in prompt template, not making a standalone claim. Worth a native
  read-through.
- Whether **"motore vocale"** (my choice for "speech engine") reads as clearly as intended
  everywhere it is embedded in longer sentences (e.g. inside `Settings_Engine_Description`),
  versus the fuller "motore di riconoscimento vocale" — I chose the shorter form
  everywhere for consistency and space, per the brief's compactness rule.

## Later additions

- `Settings_Prompts_BuiltInName` ("Default (verbatim transcription)", the built-in prompt's row label) → **"Predefinito (trascrizione letterale)"** — verbatim/word-for-word rendered as *letterale*, paired with the already-established *trascrizione* for transcription; no new terms introduced.
