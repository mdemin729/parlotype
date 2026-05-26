---
title: Source & target language selection (transcription + LLM translation)
status: planned
created: 2026-05-25
started:
completed:
---

# Source & Target Language Selection

## Problem

Parlotype transcribes with a **hard-coded source language** (`Language = "auto"` in
`AudioPipelineService.CacheSettingsAsync()`) and supports only **one translation target —
English** (Whisper's `TranslateToEnglish` toggle, ADR-021 / ADR-033). Users cannot:

- pin the source language to improve recognition accuracy, or
- translate transcription on the fly into a target language other than English.

## Goal

Let the user choose:

1. **Source language** — `Auto` (when the active model supports detection) **or** an explicit
   language picked from a list. *(System keyboard-layout detection is a future enhancement.)*
2. **Target language** — `Default` (verbatim, no translation) **or** translate the output into a
   chosen target language.

Selection must be **engine-aware**: the picker only offers what the active engine can actually do
(see capability matrix), and the recently used languages float to the top for fast reuse.

## Inputs

- **Source language:**
  - `auto` — automatic detection (only offered when the active model supports it).
  - explicit language — chosen from the engine's supported set (or the full SDK list as fallback).
  - *(future)* system keyboard layout as a source hint.
- A **most-recently-used** list of the **5** last-selected languages is pinned to the top of pickers.

## Outputs

- **Default** — the transcription verbatim, no language change.
- **Translation** — transcription translated into the selected target language.

## Engine capability matrix

| Pipeline | Source language | Target language | Status |
|---|---|---|---|
| 1. Whisper → text | Auto or explicit (Whisper's ~99 languages) | Same as source **or** English (`TranslateToEnglish`) | Source-select: **plan now**; EN-translate already exists |
| 2. Whisper → LLM (no ASR) → text | from Whisper | Any LLM-supported language | **Future** (documented, not implemented) |
| 3. LLM-with-ASR (Gemma 4) → text | Auto or explicit | Any LLM-supported language via prompt | **Plan now** |

> **Whisper limitation:** Whisper.net can translate *to English only*. Arbitrary target
> languages (e.g. ru→fr) require an LLM engine (Gemma 4 today). The UI must reflect this:
> when Whisper is the active engine, the target options are limited to `Default` / `English`.

## Scope decisions (confirmed)

- **Deliverable of *this* work item:** planning documentation only (no code).
- **Source input:** Auto + manual language list now; keyboard-layout detection later.
- **Translation scope:** Whisper = English-only; Gemma 4 = arbitrary target via prompt;
  Whisper→LLM (ASR-less) pipeline documented as future.
- **Recent-languages MRU:** 5 entries.

## Out of scope / future

- System keyboard-layout source detection (Win32 `GetKeyboardLayout`, platform-specific).
- Pipeline 2: Whisper transcription → standalone LLM translation stage (`ITextProcessor`).
- Cloud / online LLM translation providers (opt-in, per ADR-032 posture).

## Where the work lands

See [implementation-plan.md](implementation-plan.md) for the phased plan, the contracts to add,
and the files to touch.

## Verification (for the eventual implementation)

- `dotnet build Parlotype.slnx` clean (zero warnings); `dotnet test` green.
- Whisper: selecting an explicit source language changes `WhisperOptions.Language` (verify via
  log line / `TranscriptionResult.DetectedLanguage`); English translate still gated by
  `SupportsTranslation`.
- Gemma 4: selecting a target language produces translated output in that language.
- MRU: the 5 most recently used languages appear at the top of the picker and persist across restarts.
- New Core contracts → ADR(s) created; memory vault updated.
