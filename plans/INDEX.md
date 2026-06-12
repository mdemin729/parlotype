# Plans

## Completed

| Plan | Completed | Description |
|------|-----------|-------------|
| [2026-06-08-language-ux-rebuild](2026-06-08-language-ux-rebuild/) | 2026-06-11 | Language UX rebuild (ADR-036, supersedes ADR-035 UX): keyboard-layout source (`IKeyboardLayoutService` + Win32 P/Invoke), `TranslationForm` model-driven target forms (toggle/full/none), shared `LanguageRelationshipViewModel`, floating popover pickers, summary + engine-switch fallback toasts, Transcribe quick-picker strip + flyout |
| [2026-05-25-translation-model-capability](2026-05-25-translation-model-capability/) | 2026-05-25 | Gate Whisper translation by model capability (`SupportsTranslation`); disable toggle + model-list hint for `*En` and Large v3 Turbo, preserving user preference (ADR-033) |
| [2026-05-25-language-selection](2026-05-25-language-selection/) | 2026-05-25 | Source & target language selection: source picker (both engines) + Gemma 4 arbitrary-target translation; `LanguageCatalog`/`LanguageCapabilities`/`RecentLanguages` (ADR-034) |
| [2026-05-31-language-settings-ux-redesign](2026-05-31-language-settings-ux-redesign/) | 2026-05-31 | Unified Language page: `[Source] → [Target]` row with arrow as translation toggle; reusable inline `LanguagePickerView`; `TranslationEnabled` master key + per-role MRU; `LanguageSettingsMigrator` for legacy state; Whisper-output translate toggle removed (ADR-035) |

## In Progress

| Plan | Started | Description |
|------|---------|-------------|
| _none_ | | |

## Planned

| Plan | Created | Description |
|------|---------|-------------|
| [2026-05-01-pipeline-settings-alignment](2026-05-01-pipeline-settings-alignment/) | 2026-05-01 | Align pipeline defaults with ADR-011 benchmark recommendations (Medium model, language=en, beam=1) |
