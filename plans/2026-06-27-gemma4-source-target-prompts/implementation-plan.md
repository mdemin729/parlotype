# Implementation Plan — Source & Target Languages in Gemma 4 Prompts

Companion to [task.md](task.md). Layered Core → Platform → Desktop, contract-first.

## Affected code (current state)

| File | Role today | Change |
|------|-----------|--------|
| `src/Parlotype.Core/Speech/PromptTemplate.cs` | Single `Text` + `{language}` token + `Render(language)` | Retire `{language}`; add `{speech_lang}`/`{text_lang}` tokens; add **optional** `TranslationText` + `AutoDetectText` (built-in default only, nullable); 2-arg render |
| `src/Parlotype.Platform/Speech/JsonPromptTemplateRegistry.cs` | Built-in default (single `Text`); load/save/merge user prompts | Built-in default gains translation + auto-detect bodies and uses `{speech_lang}`/`{text_lang}`; **user prompts unchanged** (load/save untouched) |
| `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` (`BuildPromptTextAsync`, lines ~226-256) | Renders `{language}`=source; appends hard-coded translation sentence gated on `TranslationEnabled` | Implement the Decision-4 selection matrix; built-in uses its 3 bodies, custom uses single body + append; **keep** reading `TranslationEnabled` as the toggle gate |
| `src/Parlotype.Desktop/Views/Settings/PromptSettingsView.axaml` | One prompt-text `TextBox` + tip + intro copy | **Editor unchanged**; intro/tip copy updated to `{speech_lang}` + auto-translation explanation |

> The custom-prompt editor (`PromptSettingsViewModel`) and `prompts.json` schema
> are **not** changed — custom prompts stay single-body, so no extra field and no
> on-disk migration. (Old custom prompts containing `{language}` keep that literal
> text — accepted.)

## Token scheme (resolved — Q1)

- **`{speech_lang}`** — the spoken (source) language name; renders to
  "the detected language" when the source is auto-detect.
- **`{text_lang}`** — the output (target) language name; meaningful only in the
  built-in default's translation body.
- **`{language}` is retired** — no alias. `PromptTemplate` keeps a constant per
  token name; render replaces both.

## Phase 0 — Confirm design seams ✓ (resolved)

- [x] Q1 token naming → `{speech_lang}` / `{text_lang}`, no `{language}` alias.
- [x] Q2 built-in bodies → three bodies (see task.md "Resolved decisions").
- [x] Q3 auto-detect wording → `{speech_lang}` → "the detected language".
- [ ] Decide field names on `PromptTemplate` (proposal: `Text` = transcription body,
      `TranslationText` nullable, `AutoDetectText` nullable) and camelCase JSON
      property names.

## Phase 1 — Core contract (`PromptTemplate`)

- [ ] Replace `LanguageToken`/`DefaultLanguage` with `SpeechLanguageToken =
      "{speech_lang}"` and `TextLanguageToken = "{text_lang}"`. Define the
      auto-detect fallback string ("the detected language") as a constant.
- [ ] Add **optional** `TranslationText` and `AutoDetectText` (nullable) to the
      record. `Text` is the transcription / single body. Custom prompts leave the
      two optional bodies null.
- [ ] Provide a render path that substitutes `{speech_lang}` (and `{text_lang}`
      where present) from supplied names; the caller passes "the detected language"
      for `{speech_lang}` when auto.
- [ ] **Tests** (`Parlotype.Tests/Speech/…`): `{speech_lang}`/`{text_lang}`
      substitution; auto fallback to "the detected language"; optional bodies null
      by default; `{language}` no longer substitutes.

## Phase 2 — Registry built-in (`JsonPromptTemplateRegistry`)

- [ ] Update `BuiltInDefault`: `Text` = transcription body (Q2), add
      `TranslationText` and `AutoDetectText` (Q2 wording), all using the new tokens.
- [ ] **No migration / load-save change** for user prompts — confirm they still
      round-trip as single-body and that absent `translationText`/`autoDetectText`
      deserialize to null. Preserve corrupt-file quarantine.
- [ ] **Tests** (`JsonPromptTemplateRegistryTests.cs`): built-in default exposes
      non-null translation + auto-detect bodies; legacy single-`text` user JSON
      loads with both optional bodies null; new JSON without the fields round-trips;
      quarantine still works. Update fixtures referencing `{language}`.

## Phase 3 — Recognizer selection logic (`LlamaCppSpeechRecognizer`)

- [ ] In `BuildPromptTextAsync`, implement the Decision-4 matrix:
  - resolve source (keyboard sentinel → `IKeyboardLayoutService.Detect`, then
    `SourceLanguageResolver.Resolve`; auto-detect ⇒ unknown) and read
    `SelectedTargetLanguage` (real = not no-translation sentinel) **and**
    `TranslationEnabled` (the toggle);
  - `speechName` = source English name, or "the detected language" when auto;
  - `translationNeeded` = `TranslationEnabled` AND target real AND (source auto OR
    target ≠ source);
  - **built-in default** (has optional bodies):
    - not translationNeeded AND source auto ⇒ `AutoDetectText`;
    - translationNeeded ⇒ `TranslationText` rendered with `speechName` + target name;
    - else ⇒ `Text` rendered with `speechName`;
  - **custom prompt** (single body):
    - render `Text` with `speechName`;
    - if translationNeeded ⇒ append the existing code translation sentence (target).
  - **Keep** the `SettingsKeys.TranslationEnabled` read (R6) — it is the toggle gate.
- [ ] Keep it a single llama-server call (N1); no signature change.
- [ ] **Tests** (`LlamaCppPromptBuildingTests.cs`): rewrite for the matrix (toggle
      ON unless noted) —
      - custom, source=ru, target=none ⇒ body, ru, no append;
      - custom, source=ru, target=en, toggle ON ⇒ body, ru + appended translate-to-en;
      - custom, source=ru, target=en, toggle OFF ⇒ body, ru, no append;
      - custom, source=auto, target=none ⇒ body, "the detected language";
      - built-in, source=ru, target=none ⇒ transcription body, ru;
      - built-in, source=ru, target=en, toggle ON ⇒ translation body, ru→en;
      - built-in, source=ru, target=en, toggle OFF ⇒ transcription body, ru;
      - built-in, source=auto, target=none ⇒ auto-detect body (no tokens);
      - built-in, source=auto, target=fr, toggle ON ⇒ translation body, "the detected language"→fr;
      - built-in, source=auto, target=fr, toggle OFF ⇒ auto-detect body;
      - source=en, target=en, toggle ON ⇒ no translation (R4).

## Phase 4 — Desktop copy only (`PromptSettingsView`)

- [ ] **No editor changes** — custom prompts remain single-body.
- [ ] Update the page intro and tip text (currently "The `{language}` placeholder
      is replaced with the source language…") to use `{speech_lang}` and explain
      that a translation instruction is added automatically when the speech and
      output languages differ.
- [ ] Update `MockPromptTemplateRegistry` and prompt VM / screenshot tests that
      assert on `{language}` to the new token.

## Phase 5 — Validation

- [ ] `dotnet build Parlotype.slnx` clean (zero warnings).
- [ ] `dotnet test src/Parlotype.Tests` and `dotnet test src/Parlotype.Desktop.Tests`.
- [ ] Manual smoke: Gemma 4 — en→en (transcription), en→fr (translation), and
      auto→none (auto-detect) each produce the expected prompt (verify via the debug
      log line in `TranscribeAsync`).

## Phase 6 — Docs & memory (Definition of Done)

- [ ] **ADR** (`docs/decisions/0NN-gemma4-source-target-prompts.md`):
      `{speech_lang}`/`{text_lang}` token rename (retiring `{language}`); built-in
      default gains optional translation + auto-detect bodies; custom prompts stay
      single-body with a code-appended translation sentence; the Decision-4
      selection matrix; the `TranslationEnabled` toggle is **kept** for Gemma 4
      (same as Whisper). Note it **amends ADR-030** (single-token seam) and
      **ADR-034** (appended translation instruction). Add a row to
      `memory/decisions/_index.md`.
- [ ] Update `memory/services/core.md` (`PromptTemplate` tokens + optional
      `TranslationText`/`AutoDetectText`), `memory/services/platform.md`
      (`BuildPromptTextAsync` matrix; built-in three bodies; keeps the
      `TranslationEnabled` toggle), and `memory/architecture/subsystems.md` (Gemma
      prompt wiring: 3-body selection + toggle, same gate as Whisper).
- [ ] Mark this plan completed in `plans/INDEX.md`; capture any non-derivable
      facts in `memory/knowledge/` if discovered.

## Risks

- **Token rename is a breaking change for prompt text.** Retiring `{language}`
  means any user prompt (or doc/test fixture) using it stops substituting. Sweep
  the repo for `{language}` and update all built-in/test/UI occurrences; the user
  has accepted the impact on previously-saved custom prompts.
- **Translation toggle parity with Whisper.** The `TranslationEnabled` toggle now
  gates both engines; keep the existing UI wiring (`LanguageRelationshipViewModel`,
  arrow connector, target picker) untouched and ensure the Gemma path honours the
  toggle exactly like Whisper (toggle OFF ⇒ no translation even with a target set).
- **Auto-detect handling.** Multiple interacting cases (auto±target, known±target,
  toggle on/off) — cover each row of the matrix with a test so the auto-detect body
  vs. translation-body-with-fallback split doesn't regress.
- **Two code paths for translation.** Built-in (translation body) vs custom
  (appended sentence) must stay consistent in tone/instruction; cover both with
  tests so they don't drift.
