---
status: accepted
date: 2026-06-27
---

# 037. Source & Target Languages in Gemma 4 Prompts

## Context

The Gemma 4 (llama.cpp) engine builds its transcription prompt from a user-editable
`PromptTemplate`. Until now a prompt had one `Text` body with a single `{language}`
placeholder rendered with the **source** language (ADR-030 seam), and translation
was bolted on *outside* the template: `LlamaCppSpeechRecognizer.BuildPromptTextAsync`
appended a hard-coded "Then translate the transcript into X…" sentence whenever the
`TranslationEnabled` toggle was on and a non-`none` target was selected (ADR-034).

Two problems: (1) the template was unaware of the language *pair* — it only ever
named the source, and the same body was used whether transcribing or translating;
(2) the single `{language}` token could not express the distinct phrasings needed
for transcription, translation, and auto-detect. We want the built-in default to
carry purpose-specific bodies and the recognizer to pick the right one from the
resolved source/target pair, while keeping prompt authoring simple for users.

## Decision

1. **Token rename — `{speech_lang}` / `{text_lang}`, no `{language}` alias.**
   `{speech_lang}` = the spoken (source) language; `{text_lang}` = the output
   (target) language. The legacy `{language}` token is **retired** (no alias) —
   previously-saved custom prompts containing `{language}` no longer substitute
   (accepted). `PromptTemplate.DefaultLanguage`/`LanguageToken` are removed;
   `PromptTemplate.AutoDetectedLanguageName = "the detected language"` is the value
   used for `{speech_lang}` when the source is auto-detect. A static
   `PromptTemplate.Substitute(body, speech?, text?)` does the replacement.

2. **Three bodies on the built-in default only.** `PromptTemplate` gains two
   **optional** nullable bodies: `TranslationText` (`{speech_lang}` + `{text_lang}`)
   and `AutoDetectText` (no tokens), alongside `Text` (transcription, `{speech_lang}`).
   The built-in default (`JsonPromptTemplateRegistry.BuiltInDefault`) populates all
   three. **Custom prompts stay single-body** — both optional bodies are `null`, so
   `prompts.json` is structurally unchanged and no on-disk migration is required.

3. **Automatic selection in `BuildPromptTextAsync`.** Resolve the source (keyboard
   sentinel → `IKeyboardLayoutService.Detect`, then `SourceLanguageResolver`;
   auto-detect ⇒ unknown) and target. *Translation needed* when
   `SettingsKeys.TranslationEnabled` is **on**, a real target is set (not the
   no-translation sentinel), **and** the source is auto-detect OR target ≠ source.
   Matrix for the built-in default:

   | Speech (source) | Target | Toggle | Body |
   |---|---|---|---|
   | Known *X* | none / = *X* | any | Transcription |
   | Known *X* | *Y* ≠ *X* | OFF | Transcription |
   | Known *X* | *Y* ≠ *X* | ON | Translation |
   | Auto-detect | none | any | Auto-detect |
   | Auto-detect | *Y* | OFF | Auto-detect |
   | Auto-detect | *Y* | ON | Translation (`{speech_lang}` → "the detected language") |

   A **custom** prompt renders its single body (`{speech_lang}` = source name, or
   "the detected language" when auto) and, when *translation needed*, gets the same
   hard-coded translation sentence appended as before.

4. **Translation toggle kept for Gemma 4 (parity with Whisper).** `BuildPromptTextAsync`
   still reads `SettingsKeys.TranslationEnabled` — the one-click enable/disable is
   convenient and behaves the same on both engines. (An earlier draft dropped the
   toggle for Gemma; that was reversed.) The shared toggle, the target picker, and
   `LanguageRelationshipViewModel` UI wiring are untouched. Whisper's English-only
   translation gate (ADR-033/035) is unchanged.

Amends ADR-030 (single-token seam) and ADR-034 (appended translation instruction).

## Consequences

### Easier
- The built-in default gives the model purpose-specific instructions for
  transcription, translation, and auto-detect, instead of one generic body plus an
  appended sentence.
- Custom prompts remain a single text box — no added authoring burden, no schema
  change, no migration.
- `{speech_lang}` / `{text_lang}` read more intuitively for prompt authors than
  "source/target" jargon.

### Harder / trade-offs
- **Breaking for prompt text:** any saved custom prompt (or external doc) using the
  retired `{language}` token stops substituting. Accepted; users can re-edit.
- Two translation code paths now coexist — the built-in `TranslationText` body and
  the custom code-appended sentence — and must be kept consistent; covered by tests.
- The built-in default's three bodies live in code; changing the prescribed wording
  is a code change, not a settings edit.

### Notes
- Single LLM call preserved (transcription + translation in one request); no model
  reload on prompt change.
- Tests: `LlamaCppPromptBuildingTests` covers every matrix row (toggle on/off, auto
  vs known source, custom vs built-in); `JsonPromptTemplateRegistryTests` covers the
  three built-in bodies and legacy single-body JSON loading with null optional bodies.

## Amendment (2026-08-01) — `{text_lang}` is substituted on every path

As originally implemented, only the built-in `TranslationText` branch of
`BuildPromptTextAsync` passed a target language to `PromptTemplate.Substitute`; the
other three branches passed the speech language alone, and `Substitute` leaves a token
untouched when its argument is `null`. A **custom** prompt containing `{text_lang}`
therefore shipped the literal string `{text_lang}` to the model on all of them — with
translation off *and*, more damagingly, with translation on, which is exactly when a
prompt author would reach for the token. The built-in default never hit this: its
transcription and auto-detect bodies carry no `{text_lang}`.

Decision: `{text_lang}` is substituted on every path. While translating it renders to
the target language (custom bodies now, as the built-in translation body already did);
otherwise the output language *is* the spoken language, so it renders to the same name
as `{speech_lang}`, "the detected language" included. `Substitute` itself is unchanged
— the null-argument contract still means "leave the token alone"; only the recognizer
now supplies both arguments. Built-in behaviour is byte-identical; only custom prompts
using `{text_lang}` change, and strictly for the better. `LlamaCppPromptBuildingTests`
covers both the translating and non-translating custom-prompt cases.

All three bodies of the built-in default also gained an explicit `Use punctuation.`
sentence (placed after the transcribe/translate instruction, before "Only output…"),
plus the pre-filled text for a **new** custom prompt. Empirically the model punctuates
inconsistently without it. This is a deliberate divergence from Google's prescribed
template, which omits the instruction; `JsonPromptTemplateRegistryTests` pins it so a
future edit cannot drop it silently. The built-in is merged at read time and never
written to `prompts.json`, so every install picks the new wording up on next launch
with no migration — existing *custom* prompts (including copies made from the built-in
earlier) keep their own text and must be edited by hand.

The Prompts settings page gained a collapsible **How prompts work** panel documenting
the two placeholders, the three conditions that trigger translation, and the built-in's
three bodies versus a custom prompt's single body — the transcription body's doubled
`{speech_lang}` had been read as a typo. Panel state is session-only
(`PromptSettingsViewModel.IsHelpExpanded`), collapsed by default.
