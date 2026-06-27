---
title: "Session: 2026-06-27 — Gemma 4 source/target prompts (ADR-037)"
type: session
status: active
tags: [gemma4, prompts, llamacpp, translation, language, planning, adr-037]
created: 2026-06-27
summary: "Designed (requirements + plan, iterated 4× with user) and fully implemented source/target language awareness for the Gemma 4 prompt path. Retired the {language} token for {speech_lang}/{text_lang}; built-in default PromptTemplate now has 3 bodies (transcription/translation/auto-detect) while custom prompts stay single-body (code-appended translation, no prompts.json migration). BuildPromptTextAsync selects the body via a source/target matrix; the TranslationEnabled toggle is KEPT for Gemma 4 (parity with Whisper). Build clean (0 warn); 356 Core/Platform + 214 Desktop tests green. ADR-037 + vault updated. Plan completed. Work UNCOMMITTED."
---

# Session: 2026-06-27 — Gemma 4 source/target prompts (ADR-037)

## Active Focus

Feature: make the Gemma 4 (llama.cpp) prompt path language-pair aware. Plan folder
`plans/2026-06-27-gemma4-source-target-prompts/` (task.md + implementation-plan.md,
status: completed). **Working tree uncommitted.**

Files changed:
- `src/Parlotype.Core/Speech/PromptTemplate.cs` — retired `{language}`/`DefaultLanguage`/
  `LanguageToken`; added `SpeechLanguageToken = "{speech_lang}"`,
  `TextLanguageToken = "{text_lang}"`, `AutoDetectedLanguageName = "the detected language"`,
  static `Substitute(body, speech?, text?)`, instance `Render(speech?, text?)`, and two
  **optional nullable** bodies `TranslationText` / `AutoDetectText` (positional record params
  after `IsBuiltIn`).
- `src/Parlotype.Platform/Speech/JsonPromptTemplateRegistry.cs` — `BuiltInDefault` now sets all
  3 bodies with the new tokens.
- `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` — `BuildPromptTextAsync` rewritten
  to the selection matrix (keeps `TranslationEnabled` read).
- `src/Parlotype.Desktop/Views/Settings/PromptSettingsView.axaml` — intro + tip copy → `{speech_lang}`.
- `src/Parlotype.Desktop/ViewModels/Settings/PromptSettingsViewModel.cs` — `NewPrompt` default text
  uses `PromptTemplate.SpeechLanguageToken`.
- Tests: `LlamaCppPromptBuildingTests` (rewritten — full matrix), `JsonPromptTemplateRegistryTests`
  (3-body + legacy single-body JSON load), `PromptSettingsViewModelTests`,
  `PromptSettingsScreenshotTests`, `Mocks/MockPromptTemplateRegistry`.
- Docs/vault: `docs/decisions/037-gemma4-source-target-prompts.md` (new); `memory/decisions/_index.md`,
  `memory/services/core.md`, `memory/services/platform.md`, `memory/architecture/subsystems.md`;
  `plans/INDEX.md` (moved to Completed).

## Decisions Made

User-confirmed design (iterated across the session):
- **Tokens:** `{speech_lang}` (source) + `{text_lang}` (target). `{language}` **retired, no alias** —
  old custom prompts with `{language}` now render literally (accepted). Maintainer map: speech=source,
  text=target.
- **Three bodies on the built-in default only.** Custom prompts keep one body; translation for them is a
  code-appended sentence (today's behavior). Optional bodies null ⇒ `prompts.json` unchanged, no migration.
- **Selection rule (matrix).** *Translation needed* = `TranslationEnabled` ON **AND** real target
  (not `none`/blank) **AND** (source auto-detect **OR** target ≠ source). Built-in: auto+no-translation ⇒
  `AutoDetectText`; translation ⇒ `TranslationText`; else `Text`. Custom: `Text` + append when needed.
  `{speech_lang}` → "the detected language" when source is auto.
- **Kept the `TranslationEnabled` toggle for Gemma 4** (parity with Whisper) — reversed an earlier
  "drop the toggle" draft. One-click enable/disable beats re-selecting a language. UI / `LanguageRelationshipViewModel`
  wiring untouched; Whisper path untouched.

## Facts Learned

- `prompts.json` serializes `List<PromptTemplate>` by camelCase property name, so adding **optional**
  nullable positional record params (`translationText`/`autoDetectText`) is backward-compatible — absent
  keys deserialize to null; no schema migration needed.
- `BuildPromptTextAsync` is the right (and only) seam for Gemma translation gating — `AudioPipelineService`,
  `LanguageRelationshipViewModel`, and `LanguageSettingsMigrator` did not need to change.
- `TranslationEnabled` is shared by both engines; for Whisper it's `TranslateToEnglish = TranslationEnabled &&
  target=="en" && SupportsTranslation`, for Gemma it now gates the body-selection matrix.
- Incremental `dotnet build Parlotype.slnx` reports the 3 pre-existing `AVLN5001` AXAML obsolete-API warnings
  only when AXAML recompiles; they are not errors and unrelated to this work.

## Open Blockers

- **Working tree uncommitted** on branch `master`. Scope is clean — `git status` shows
  only this feature's files (no leftover keyboard-layout/`fix_language_seettings_bugs`
  changes; that prior work is already merged). New: `docs/decisions/037-*`,
  `plans/2026-06-27-gemma4-source-target-prompts/`, this session note. Ready to commit as one unit.
- Unrelated **NU1903** advisory (transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 in Benchmark) still breaks the
  full-solution warnings-as-errors build; build/test here used per-project + `-p:EnableCuda=false` and stayed green.
  Not addressed.

## Documentation Status

- ADR: **done** — `docs/decisions/037-gemma4-source-target-prompts.md` (amends ADR-030 + ADR-034). Triggered by
  Core record change + Whisper/Gemma prompt subsystem touch.
- Vault (services/architecture): **done** — `decisions/_index.md` row 037; `services/core.md` (PromptTemplate),
  `services/platform.md` (BuildPromptTextAsync), `architecture/subsystems.md` (pipeline wiring).
- Knowledge (non-derivable facts): **none required** — design is fully captured in ADR-037 + vault and is
  code-derivable. Stored one repository `store_memory` on the token convention.

## Next Action

Commit the Gemma 4 source/target-prompt work on `master` (no AI-attribution trailer per user preference).
Scope is self-contained (15 modified + 3 new paths, all this feature). Suggested message covers: token rename
`{language}`→`{speech_lang}`/`{text_lang}`, 3-body built-in `PromptTemplate`, matrix-based `BuildPromptTextAsync`
(toggle kept), ADR-037 + vault. Optionally decide separately on the unrelated Benchmark NU1903 advisory.
