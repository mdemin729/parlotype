---
title: "Session: 2026-05-31 — Language Settings UX Redesign"
type: session
status: active
tags: [language, translation, settings, ux, avalonia, adr-035]
created: 2026-05-31
summary: "Unified Language settings page: [Source] → [Target] row with arrow-as-toggle, inline reusable picker, new TranslationEnabled key + per-role MRU, idempotent legacy migrator, Whisper-output translate toggle removed (ADR-035)"
---

# Session: 2026-05-31 — Language Settings UX Redesign

## Active Focus

End-to-end implementation of plan [`2026-05-31-language-settings-ux-redesign`](../../plans/2026-05-31-language-settings-ux-redesign/) — Phases 0 through 5 plus a high-effort code review pass.

**New files**
- `src/Parlotype.Core/Speech/LanguageSettingsMigrator.cs` — idempotent one-shot migrator from legacy `TranslateToEnglish` + shared `RecentLanguages` to `TranslationEnabled` + per-role MRUs. Single-guard (presence of `TranslationEnabled` key).
- `src/Parlotype.Desktop/ViewModels/LanguagePickerViewModel.cs` — reusable picker (header + search + items + `IsOpen`); callback-driven so it's host-agnostic.
- `src/Parlotype.Desktop/ViewModels/LanguageRowFactory.cs` — pure presentation helper for ordering/filtering rows (sentinel → recents → catalog).
- `src/Parlotype.Desktop/Views/Settings/LanguagePickerView.axaml(.cs)` — view; self-hosts visibility via `IsOpen`.
- `src/Parlotype.Tests/LanguageSettingsMigratorTests.cs` — 10 cases covering legacy→new transitions, idempotency, MRU seeding.
- `docs/decisions/035-language-settings-ux-redesign.md`.

**Rewritten files**
- `src/Parlotype.Core/Settings/SettingsKeys.cs` — added `TranslationEnabled`, `RecentSourceLanguages`, `RecentTargetLanguages`; legacy keys marked.
- `src/Parlotype.Core/Speech/LanguageCatalog.cs` — added `EnglishCode` constant + `GetDisplayLabel(code)` helper.
- `src/Parlotype.Core/Speech/LanguageCapabilities.cs` — Whisper `FixedTranslationTargets = [English]`.
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — derives `WhisperOptions.TranslateToEnglish` from `TranslationEnabled` + target = `en` + model capability; calls migrator at the top of `CacheSettingsAsync`.
- `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` — `BuildPromptTextAsync` gates the in-prompt translation instruction on `TranslationEnabled`; no migrator call on the hot path (defense-in-depth removed in review).
- `src/Parlotype.Desktop/ViewModels/Settings/LanguageSelectionSettingsViewModel.cs` — `OpenPicker` enum + observable derivatives, `TranslationEnabled`, `ShowTranslationPausedNote`, `SourceButtonLabel`/`TargetButtonLabel`, single `TogglePicker` helper, single `SelectInto` helper for source/target.
- `src/Parlotype.Desktop/Views/Settings/LanguageSelectionSettingsView.axaml` — top row `[Source] → [Target]` with arrow button as toggle; child pickers self-host visibility.
- `src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs` + view — `Translate to English` toggle removed.
- `src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs` — Whisper-model change now flows to `Language.UpdateTranslationAvailability` (was `WhisperOutput.UpdateTranslationAvailability`).
- Tests across `Parlotype.Tests` (audio pipeline + Llama prompt + migrator) and `Parlotype.Desktop.Tests` (language section + Whisper output + settings window + screenshot scenarios) — full rewrites for the affected suites; `LanguageSettingsScreenshotTests` added.

## Decisions Made

- **Arrow (→) IS the translation toggle.** No separate `ToggleSwitch`. Disabled state styled via `Classes.off`; target button disabled via `IsEnabled`.
- **One picker visible at a time.** `OpenPicker` enum (`None`/`Source`/`Target`) drives a single inline `ContentControl`-like layout (two `LanguagePickerView` instances, each gated by `IsOpen` on its own VM).
- **Picker is host-agnostic.** `LanguagePickerViewModel.IsOpen` lives on the picker; parent's `OnOpenPickerChanged` partial routes the bool. View no longer uses `$parent[UserControl]` traversal binding.
- **`Translate to English` toggle removed** from Whisper-output section. Replaced by deriving `WhisperOptions.TranslateToEnglish = TranslationEnabled && target == "en" && model.SupportsTranslation` in `AudioPipelineService.CacheSettingsAsync`.
- **Per-role MRU.** `RecentSourceLanguages` and `RecentTargetLanguages` replace the shared list. The legacy shared list seeds the source MRU on first run; target MRU starts empty (legacy list mixed both roles).
- **Single-guard idempotency.** `LanguageSettingsMigrator.MigrateAsync` short-circuits after one `GetAsync` once `TranslationEnabled` is written. Run from `AudioPipelineService.CacheSettingsAsync` and `LanguageSelectionSettingsViewModel.InitializeAsync`; removed from `LlamaCppSpeechRecognizer.BuildPromptTextAsync` (hot path) during review.
- **`LanguageCatalog.EnglishCode`** is the new constant for `"en"` — used by capabilities, migrator, pipeline, VM, tests.
- **`LanguageCatalog.GetDisplayLabel(code)`** is the single source of truth for the `"English — Native"` formatter; replaces the per-VM `LabelOrCode` and `LanguageRowFactory.Label`.
- **Whisper publishes `[English]` as `FixedTranslationTargets`** so the unified picker renders Whisper's target like any other engine.

## Facts Learned

- **Avalonia `[ObservableProperty]` source generator (CommunityToolkit.Mvvm)** uses `SetProperty` with `EqualityComparer<T>.Default` — same-value writes are free and don't raise `PropertyChanged`. So `UpdateTranslationAvailability(model)` setting `WhisperModelSupportsTranslation` unconditionally is OK.
- **`JsonSettingsService.GetAsync` re-reads `settings.json` from disk on every call** (no in-memory cache). This makes any per-transcription `GetAsync` chain expensive. Out of scope to fix here; flagged for a follow-up. Confirmed by the efficiency reviewer.
- **The `.NET Host` process locks build outputs on Windows.** Standard CLAUDE.md guidance: kill PID then rebuild. Encountered multiple times during this session.
- **Avalonia `Classes.foo="{Binding}"` works on boolean bindings** for dynamic style activation (already used for `Classes.recording`; reused for `Classes.active` on language buttons and `Classes.off` on the arrow).
- **`ToolTip.Tip="..."`** is sufficient for arrow-toggle discoverability; a full `AutomationProperties.Name` would be polish for an accessibility pass.

## Open Blockers

None. The plan is complete and all 579 tests pass.

**Follow-ups noted but deferred:**
- `JsonSettingsService` in-memory caching (out of scope; pre-existing tech debt).
- Shared in-memory `ISettingsService` test double across `Parlotype.Tests` and `Parlotype.Desktop.Tests` (three nearly-identical impls today; would require `InternalsVisibleTo` or a new shared test-utilities project).
- Legacy `SettingsKeys.TranslateToEnglish` / `RecentLanguages` constants can be deleted entirely after a release window.
- `ItemsRepeater` / virtualizing panel for the picker (~250-row `ItemsControl` rebuilds on each keystroke; not a real problem today but a clear next step if profile data shows it).

## Documentation Status

- ADR: done — [`docs/decisions/035-language-settings-ux-redesign.md`](../../docs/decisions/035-language-settings-ux-redesign.md)
- Vault (services/architecture): done — `memory/services/core.md`, `memory/services/desktop.md`, `memory/architecture/subsystems.md` (new **Language & Translation** subsection), `memory/decisions/_index.md`
- Knowledge (non-derivable facts): none required — the facts above are either derivable from current code or already covered by existing knowledge files

## Next Action

Pick up the next plan from `plans/INDEX.md`. The remaining **Planned** row is:

> [`2026-05-01-pipeline-settings-alignment`](../../plans/2026-05-01-pipeline-settings-alignment/) — Align pipeline defaults with ADR-011 benchmark recommendations (Medium model, language=en, beam=1).

That plan now interacts with the new `TranslationEnabled` model: if defaults move to "language=en" (source), no behavior change. If "translate=on" is part of the default, it now means setting both `TranslationEnabled = true` and `SelectedTargetLanguage = "en"`.
