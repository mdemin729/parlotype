---
title: Source & Target Languages in Gemma 4 Prompts
status: completed
created: 2026-06-27
started: 2026-06-27
completed: 2026-06-27
---

# Source & Target Languages in Gemma 4 Prompts

## Problem

The Gemma 4 (llama.cpp) engine drives transcription from a user-editable
`PromptTemplate` whose `Text` contains a single `{language}` placeholder, rendered
with the **source** language (ADR-030 seam, ADR-034 wiring). Translation into an
arbitrary target language is bolted on *outside* the template: when a target is
selected and the shared `TranslationEnabled` toggle is on,
`LlamaCppSpeechRecognizer.BuildPromptTextAsync` **appends a hard-coded sentence**
("Then translate the transcript into X…") after the rendered prompt.

This has two shortcomings:

1. **The template is unaware of translation.** A prompt only ever describes
   transcription; the translation instruction is fixed in code, not authorable or
   tunable per prompt. Source-only prompts cannot express how the model should
   transcribe-then-translate.
2. **The template ignores the language pair.** It only ever renders the source
   language; the target plays no part in the template, and the same single body is
   used whether transcribing or translating. The model's mental model is simpler:
   if the spoken and output languages are the same, transcribe; if they differ
   (and translation is enabled), translate. The translation toggle itself is a
   useful one-click control and is **kept** (same as Whisper) — see Decision 5.

## Goal

Make the Gemma 4 prompt path **language-pair aware**. The **built-in default**
prompt carries **three** bodies — transcription (known speech language),
translation (into a different language), and auto-detect (unknown speech language)
— and the recognizer chooses between them automatically from the resolved source
and target languages. **Custom prompts stay single-body**; for them, the auto and
translation cases are handled in code (`{speech_lang}` fallback + an appended
translation sentence).

## Decisions (confirmed with user)

1. **Token scheme — `{speech_lang}` / `{text_lang}`, no `{language}` alias.**
   `{speech_lang}` = the spoken (source) language; `{text_lang}` = the output
   (target) language. The legacy `{language}` token is **retired** — old custom
   prompts containing `{language}` will no longer substitute (accepted by user).
   Mapping for maintainers: speech = source, text = target.
2. **Three bodies for the built-in default only.** The built-in default
   `PromptTemplate` carries three bodies:
   - **Transcription** (`{speech_lang}`) — speech language known, no translation.
   - **Translation** (`{speech_lang}` + `{text_lang}`) — translate into a different
     language; `{speech_lang}` renders to "the detected language" when the source
     is auto-detect.
   - **Auto-detect** (no tokens) — speech language unknown, no translation.
3. **Custom prompts stay single-body.** A custom prompt has one body using
   `{speech_lang}`. The recognizer renders it with `{speech_lang}` = the source
   language name (or "the detected language" when auto), and **appends the
   translation instruction sentence in code** when translation is needed.
4. **Selection rule.** Resolve source (keyboard-layout sentinel → detect;
   auto-detect ⇒ unknown) and target (real = not the no-translation sentinel).
   *Translation needed* when the **translation toggle (`TranslationEnabled`) is
   ON**, a real target is set, **and** (source is auto-detect **or** target ≠
   source). Selection matrix for the built-in default:

   | Speech (source) | Target | Toggle | Body |
   |---|---|---|---|
   | Known *X* | none / equal to *X* | any | Transcription |
   | Known *X* | Known *Y* ≠ *X* | OFF | Transcription |
   | Known *X* | Known *Y* ≠ *X* | ON | Translation |
   | Auto-detect | none | any | Auto-detect |
   | Auto-detect | Known *Y* | OFF | Auto-detect |
   | Auto-detect | Known *Y* | ON | Translation (`{speech_lang}` → "the detected language") |

   For a **custom** prompt: render its single body (`{speech_lang}` = source name
   or "the detected language") and append the code translation sentence whenever
   *translation needed* is true.
5. **Keep the translation toggle for Gemma 4.** The `TranslationEnabled` toggle is
   retained and behaves the **same as for Whisper**: it is the one-click
   enable/disable for translation. `BuildPromptTextAsync` **keeps** reading
   `SettingsKeys.TranslationEnabled` as a gate (combined with the target ≠ source
   rule above). Rationale: toggling the button is faster than re-selecting a
   language. (This supersedes the earlier "drop the toggle" decision.)

## Requirements

### Functional

- **R1 — Built-in default has three bodies.** Transcription (`{speech_lang}`),
  Translation (`{speech_lang}` + `{text_lang}`), and Auto-detect (no tokens), with
  the wording agreed in Decision 2.
- **R1b — Custom prompts stay single-body.** Custom (user) prompts keep one body
  using `{speech_lang}`. No extra field is added to the prompt editor or
  `prompts.json` schema.
- **R2 — Token set.** `{speech_lang}` (source) and `{text_lang}` (target) only.
  `{language}` is retired; no alias.
- **R3 — Automatic selection.** `LlamaCppSpeechRecognizer` resolves source and
  target and selects the body per the Decision 4 matrix (built-in) or
  single-body-plus-append (custom).
- **R4 — Same language is no-op.** When the selected target equals the resolved
  source (e.g. `en → en`), no translation instruction is emitted.
- **R5 — Auto-detect source.** Source auto-detect + translation not needed ⇒ the
  Auto-detect body (built-in) or the custom body with `{speech_lang}` → "the
  detected language". Source auto-detect + *translation needed* (toggle ON + real
  target) ⇒ the Translation body / append path, with `{speech_lang}` → "the
  detected language".
- **R6 — Keep the Gemma translation toggle.** `BuildPromptTextAsync` **keeps**
  reading `SettingsKeys.TranslationEnabled` as a gate, behaving the same as the
  Whisper path: translation only when the toggle is ON (and target ≠ source).
- **R7 — Backward compatibility.** Existing user prompts in `prompts.json` are
  **unaffected** structurally — they remain single-body and load unchanged; the new
  translation / auto-detect bodies live only on the code-defined built-in default.
  No on-disk migration is required. (Caveat: any user prompt text containing the
  retired `{language}` token will no longer substitute — accepted.) Corrupt-file
  quarantine behavior preserved.
- **R8 — Prompt editor UI.** The custom-prompt editor is **unchanged** (single
  body). The built-in default remains read-only. The page intro / tip copy is
  updated to use `{speech_lang}` and to explain that translation is added
  automatically when speech ≠ text language.

### Non-functional / constraints

- **N1 — Single LLM call.** Transcription and translation continue to happen in
  one llama-server call (no separate ASR step). No model reload on prompt change.
- **N2 — Whisper untouched.** Whisper's source-language wiring and its English-only
  `TranslateToEnglish` gate (driven by `TranslationEnabled` + `SupportsTranslation`,
  ADR-033/035) are **unchanged**. `TranslationEnabled` is **not** removed globally.
- **N3 — Zero-warning build, all tests green** (`TreatWarningsAsErrors`).
- **N4 — Definition of Done.** A Core record change + audio/Whisper/prompt
  subsystem touch ⇒ **ADR required**; memory vault + service docs updated.

## Non-goals

- Removing or changing the `TranslationEnabled` setting for the **Whisper** path.
- Conditional/templating mini-language inside a single prompt body.
- Per-language prompt variants beyond the three built-in bodies.
- The deferred Whisper→LLM (ASR-less) translation pipeline (ADR-034 "Pipeline 2").

## Resolved decisions (formerly open questions)

- **Q1 — Token naming → resolved.** `{speech_lang}` + `{text_lang}` only; the
  legacy `{language}` token is retired (no alias).
- **Q2 — Built-in default bodies → resolved.** Three bodies (Decision 2):
  - Transcription: `Transcribe the following speech segment in {speech_lang} into
    {speech_lang} text. Only output the transcription, with no newlines. When
    transcribing numbers, write the digits.`
  - Translation: `Transcribe the following speech segment spoken in {speech_lang}
    and translate it into {text_lang}. Only output the {text_lang} text, with no
    newlines. When writing numbers, write the digits.`
  - Auto-detect: `Detect the language being spoken and transcribe the speech in
    that same language. Only output the transcription, with no newlines. When
    transcribing numbers, write the digits.`
- **Q3 — Auto-detect wording → resolved.** `{speech_lang}` renders to
  "the detected language" whenever the source is auto-detect.

See [implementation-plan.md](implementation-plan.md) for the phased workplan.
