---
status: superseded by ADR-036
date: 2026-05-31
---

# 035. Language Settings UX Redesign

> **Superseded by [ADR-036](036-language-ux-rebuild.md)** (2026-06-11): the
> inline pickers became floating popovers, the target side became model-driven
> (toggle / full / none forms), a keyboard-layout source sentinel was added, and
> the page logic moved into a shared `LanguageRelationshipViewModel` also used
> by the Transcribe window quick picker. The data model and migration from this
> ADR remain in force.

## Context

ADR-034 introduced source/target language selection: a Core language catalog
(`LanguageCatalog`), per-engine capabilities (`LanguageCapabilities`), a
recently-used MRU helper, and a dedicated **Language** settings section. The
section stacked two vertical pickers and routed Whisper's English-translation
through a *separate* `Translate to English` toggle under **Whisper output**
(ADR-021 / ADR-033).

That split caused three usability problems:

1. **Translation control was on two pages.** Users toggled translation under
   *Whisper output*, then picked the target language under *Language* — one
   intent ("translate my transcript into <target>"), two screens.
2. **Engine asymmetry leaked into the UI.** Whisper used a toggle (English-only);
   Gemma 4 used a target picker (any language). Identical user intent, different
   widgets.
3. **Two stacked pickers don't scale.** ~99 Whisper / open-ended Gemma languages
   dominated the page even though only one picker is wanted at a time.

## Decision

1. **Unified translation control on the Language page.** A single row at the top
   of the Language settings page:

   ```
   [Source language] → [Target language]
   ```

   - The **arrow (→)** is the master **translation toggle**
     (`SettingsKeys.TranslationEnabled`). When off, both arrow and target button
     render in a muted style; target button is disabled.
   - The currently-open picker's button gets a green focus border.
   - Selecting a language closes the inline picker.

2. **Reusable inline picker (`LanguagePickerView` + `LanguagePickerViewModel`).**
   One component, used twice (source / target). Contains:
   - A `Header` ("Select source language" / "Select target language").
   - A `Search languages…` text box (live filter on English / native / code).
   - The list of rows: optional leading sentinel ("Auto-detect" on source for
     engines that support detection), then the recently-used languages pinned
     to the top with a "Recent" label, then the remaining catalog in order.
   - The active row has an accent bar on the left.

3. **New settings model.**
   - `SettingsKeys.TranslationEnabled` (bool) is the source of truth for
     translation on/off.
   - `SettingsKeys.SelectedTargetLanguage` keeps its meaning but is no longer
     overloaded with "off" semantics (`"none"` is tolerated as legacy state).
   - Per-role MRU lists replace the shared one:
     `SettingsKeys.RecentSourceLanguages` and `SettingsKeys.RecentTargetLanguages`.
   - The legacy `SettingsKeys.TranslateToEnglish` and `SettingsKeys.RecentLanguages`
     keys are read once on startup for migration and never written by the new
     runtime.

4. **Pipeline wiring is settings-derived.** `AudioPipelineService.CacheSettingsAsync`
   reads `TranslationEnabled` + `SelectedTargetLanguage`. The Whisper-only
   `WhisperOptions.TranslateToEnglish` flag is computed:
   `TranslationEnabled && SelectedTargetLanguage == "en" && model.SupportsTranslation`.
   For Gemma 4, `LlamaCppSpeechRecognizer.BuildPromptTextAsync` gates the
   in-prompt translation instruction on `TranslationEnabled`.

5. **One-shot migration (`LanguageSettingsMigrator`).** Idempotent. Runs from the
   audio pipeline, the Gemma 4 prompt builder, and the Language ViewModel — any
   path that reads the new keys triggers a migration if it hasn't happened yet.
   - Legacy `TranslateToEnglish = true` ⇒ `TranslationEnabled = true`,
     `SelectedTargetLanguage = "en"` (unless an explicit target was already set).
   - Explicit non-`none` target (Gemma 4 pre-redesign) ⇒ `TranslationEnabled = true`.
   - Shared `RecentLanguages` ⇒ seed `RecentSourceLanguages`; the target MRU
     starts empty because the legacy list mixed both roles.

6. **Whisper-output cleanup.** The `Translate to English` toggle is removed from
   `WhisperOutputSettings*`. The "paused — model can't translate" hint moves to
   the Language page as `ShowTranslationPausedNote`, shown when the user has
   translation on and the active Whisper model is one of the
   non-translation-capable ones (English-only / Large v3 Turbo, ADR-033).

7. **Whisper capability publishes English as a target.**
   `SpeechEngineCapabilities.For(Whisper).FixedTranslationTargets` now contains
   English. This lets the unified picker render English like any other target
   without special-casing Whisper in the View.

## Consequences

**Easier**
- Single mental model: source → target, on/off. One screen.
- Two engines look the same to the user — same widgets, same flow.
- Source and target MRU lists no longer pollute each other.
- The new picker is reusable: any future "choose a language" surface can take
  the same `LanguagePickerView` with new callbacks.

**Harder / explicit trade-offs**
- The legacy `TranslateToEnglish` / `RecentLanguages` settings keys remain in
  `SettingsKeys.cs` (marked legacy) to support migration. A future cleanup PR
  can remove them after a release window.
- The arrow toggle is a custom interaction (button-as-toggle). The
  `ToolTip.Tip="Toggle translation"` annotation provides discoverability; a
  screen-reader accessibility name should accompany any future polish.
- `LanguageSelectionSettingsViewModel` grew (it now owns translation state,
  paused-note logic, two child picker VMs, and engine + model awareness). The
  presentation responsibilities are clear but the file is dense.

## References

- ADR-021 — Whisper translation to English
- ADR-028 — Settings grouped navigation
- ADR-033 — Translation capability per Whisper model
- ADR-034 — Source & target language selection (data model + initial UI)
