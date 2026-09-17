---
title: Translation brief — shared rules, register and core glossary
status: reference
created: 2026-09-17
---

# Translation brief

Handed to every translator of Parlotype's interface, alongside the machine-readable
`<culture>.brief.json`. Read this first; it is short on purpose.

## What Parlotype is

A local-by-default **voice-to-text desktop app** for Windows. You hold a hotkey, speak,
and the recognized text is typed into whatever app your cursor is in. Speech recognition
runs **on the user's own machine** by default — the audio never leaves it unless the user
deliberately turns on a cloud engine.

That last point is the product's whole identity, so copy about privacy, local processing
and "your audio stays here" must stay precise. Do not soften it into marketing, and do not
overstate it either: the app *can* use cloud engines, opt-in, and says so plainly.

The interface is small — a compact always-on-top widget and a settings window. Copy is
terse because there is very little room. **A translation that is twice as long as the
English is a bug**, not a stylistic choice.

## Register

- Terse. Sentence case. No exclamation marks. No marketing voice.
- Address the user directly and neutrally, in whatever way your language does that
  without choosing a formality register the English does not have. Where your language
  forces a T/V choice (`du`/`Sie`, `ty`/`vy`, `tu`/`vous`), pick the one desktop software
  in your language normally uses and **state which in your glossary** — then never mix.
- Prefer the verb form your OS uses for the same action. If Windows in your language says
  "Zatvoriť" for Close, say that.
- Use your locale's own typography: correct quotation marks, correct dashes, a
  non-breaking space where your language needs one before a unit or punctuation mark.
  State your quotation convention in your glossary.

## Never translate

These are identifiers. A user sees them spelled exactly this way elsewhere — in a file
name, in another app, on their keyboard — and translating them breaks the connection.

**Product and engine names:** Parlotype, Whisper, Parakeet TDT v3, Parakeet, Gemma 4,
sherpa-onnx, llama.cpp, llama-server, Silero VAD, Vulkan, ONNX.

**Cloud providers and models:** OpenAI, Groq, xAI, Grok, `gpt-4o-mini-transcribe`,
`whisper-large-v3`, Large v3 Turbo, and any other model identifier.

**Files, paths and technical tokens:** `settings.json`, `secrets.json`,
`%LOCALAPPDATA%`, `sk-…`, `xai-…`, WAV, GB, MB, ms.

**Keyboard key names:** Ctrl, Alt, Shift, Win, Space, Esc, Tab, Enter. These are printed
on the user's physical keyboard and Windows leaves them alone in every language. Only the
words *around* them are translated — "Hold Right Ctrl" becomes your language's way of
saying hold, plus your language's word for right, plus `Ctrl` untouched.

**Interface language names** are endonyms elsewhere in the app and are not in this file.

## Core glossary

One word per concept, for the whole file. Pick it once, write it in your glossary, and do
not reach for a synonym later because a sentence reads better — the user is learning your
vocabulary as they go, and two words for one thing costs them more than an inelegant
sentence does.

| Term | What it means here |
|---|---|
| **dictation** | The act of speaking to have text typed. The app's core verb. |
| **transcribe** | Turn recorded speech into text. Distinct from *translate*. |
| **translate** | Turn speech in one language into text in **another**. Only some engines do this. Never use your word for this to mean *transcribe*. |
| **recording** | The capture that is happening right now, while the user speaks. |
| **speech engine** | The recognizer doing the work: Parakeet, Whisper, Gemma 4, or a cloud provider. User-facing choice. |
| **model** | The downloadable weights an engine runs. An engine has several; the user picks one by size/quality. |
| **runtime** | How the engine executes — Vulkan (GPU) or CPU. Whisper only. |
| **source language** | The language the user **speaks**. |
| **target language** | The language Parlotype **types**. |
| **hotkey** | The global keyboard shortcut that starts dictation. |
| **push-to-talk** | Hold the key while speaking; release to finish. |
| **toggle** | Press once to start, again to stop. |
| **binding** | One configured gesture-plus-mode pair in the hotkey list. |
| **tray** | The Windows notification area, bottom-right. |
| **widget** | The small always-on-top window with the record button. |
| **waveform** | The live audio visualization on the widget. |
| **wait time** | How long the app keeps listening through silence before deciding the user has finished. |
| **punctuation** | Automatic commas and full stops in the recognized text. |
| **profanity filter** | Masking of profanity in the output. |
| **inject / typed into** | How the text reaches the other app. Prefer your natural phrasing for "the text is typed into the app" — do not invent a technical term for *inject*. |
| **clipboard** | The system clipboard. The app borrows and restores it. |
| **cloud engine / cloud provider** | An opt-in online recognizer. Always clearly marked as such. |
| **API key** | The user's own credential for a cloud provider. |
| **onboarding** | The first-run tour. |
| **restart required** | A change that only takes effect after the app restarts. |

## Placeholders

`{0}`, `{1}` are substituted at runtime with a name, number or path.

- **Preserve every one.** Never add one, never drop one.
- You **may** move them within the sentence — that is the point of numbering them.
- The comment on a key tells you what each one holds. Read it.
- **If your language would need to inflect the substituted word** — a case ending, a
  gendered article, an agreeing adjective — you cannot, because the value arrives
  verbatim. Translate it as best you can with the slot at the edge of the clause or after
  a colon, **and list the key in your report.** That is a signal the English phrasing
  needs changing, which is a decision for the maintainer, not something to paper over.

## Hard rules

1. Every key in the brief appears in your output. No key is left out.
2. No key is left in English. If you genuinely cannot translate one, translate it as best
   you can and list it in your report — do not paste the English through. An automated
   check rejects any value over 25 characters that matches the English exactly.
3. Placeholder sets match, per key.
4. Nothing from the "Never translate" register is translated.
5. One word per concept, matching your glossary.

## What you produce

1. `<culture>.json` — a flat JSON object, `{"Key_Name": "translation", ...}`, every key,
   nothing else. No wrapper object, no comments, no extra keys.
2. `glossary-<culture>.md` in this folder — your choice for each core-glossary term, your
   formality register, your quotation convention, and anything you were unsure about.

## A note on the key names

Keys read `Area_Component_Purpose` — `Settings_Theme_Title`,
`Transcribe_Status_Recording`, `Onboarding_Recording_Body`. Use them: the prefix tells you
where the string appears and therefore how much room it has. Anything ending in `_Title`
is a heading, `_Body` is a paragraph, `_Button` is a button, `_Format` carries
placeholders, `_Tooltip` is hover text.
