---
title: Language settings UX redesign (source/target buttons, inline picker, arrow toggle)
status: completed
created: 2026-05-31
started: 2026-05-31
completed: 2026-05-31
---

# Language Settings UX Redesign

## Problem

The current Language settings page ([LanguageSelectionSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/LanguageSelectionSettingsView.axaml))
stacks two pickers vertically and exposes the **English-translate** toggle in a *different*
section (`Whisper output`). Three problems with that arrangement:

1. **Translation control is split across two pages.** The user toggles translation in
   *Whisper output*, but picks the target language in *Language*. A single mental model
   ("source → target, on/off") is fragmented.
2. **Engine asymmetry leaks into the UI.** The Whisper target-language story (English-only,
   driven by `TranslateToEnglish`) lives under a different control than the Gemma 4 target
   picker, so the two engines behave differently in the UI even though the user intent
   ("translate my transcript into <target>") is identical.
3. **Vertical stacked pickers don't scale.** With ~99 (Whisper) / open-ended (full culture
   list) languages, the page is dominated by two long lists at once. Only one picker is
   needed at a time.

## Goal

Match the prototype frames:

- A single row at the top: **[Source language button] → [Target language button]**, where
  the arrow (→) is the **Translation enabled/disabled toggle**.
- Clicking either button **expands an inline picker** below the row (one picker visible at
  a time). The picker is a **shared component** with a search box and a per-button MRU.
- The Whisper-output "Translate to English" toggle is **removed**; its behaviour is folded
  into the unified target-language picker (Whisper target = English ⇒ `TranslateToEnglish`
  is set under the hood).

## Inputs (UX, from prototype)

- **Row:** `Source language` label + dark button | arrow button | `Target language` label + dark button.
- **Active button** (picker open): green focus border.
- **Translation disabled state:** arrow + target button rendered greyed; clicking the target
  is a no-op while disabled.
- **Picker** (shared component):
  - Heading: "Select source language" / "Select target language".
  - `TextBox` with `Search languages…` placeholder; live-filters the list.
  - List rows: `English`, `Russian - Руский`, `French - français`, …
  - Each MRU row shows the word **Recent** right-aligned (top 5).
  - The *currently selected* row is marked with a blue accent bar on the left.
  - Source picker has an `Auto-detect` row above the language list (hidden while filtering,
    per existing behaviour).
  - Whisper + target picker: only `English` is offered (engine capability).

## Inputs (engineering, from clarifying answers)

- **Toggle widget:** the arrow (→) **is** the toggle. No separate `ToggleSwitch`.
- **Whisper output section:** the `Translate to English` toggle is **removed**. Translation
  is set exclusively from the Language page from now on.
- **Picker layout:** **inline below the buttons** (matches the screenshots), not a flyout.

## Outputs

- A reusable `LanguagePickerView` UserControl (search + list, MRU-aware, engine-aware).
- A redesigned `LanguageSelectionSettingsView` using two instances of the picker plus the
  source/arrow/target row.
- A new `TranslationEnabled` setting (bool) that the Language page owns. Backward-compat
  migration from the legacy `TranslateToEnglish` boolean.
- **Separate MRU lists** for source and target: `RecentSourceLanguages` /
  `RecentTargetLanguages` (replacing the shared `RecentLanguages` key).

## Scope decisions (confirmed via `AskUserQuestion`)

- Arrow is the translation toggle. Disabled state greys both arrow and target button.
- `Translate to English` is removed from *Whisper output*. The setting is computed: Whisper +
  target=`en` ⇒ `TranslateToEnglish = true`.
- Picker is inline (not a Flyout).
- Source/target each maintain their own MRU (the current shared MRU is split).

## Out of scope

- New languages / engines / pipelines (covered by ADR-034 and follow-ups).
- Keyboard-layout source detection (still future, per ADR-034).
- Reworking the navigation pane / `SettingsCategory` taxonomy.
- Changing the data model of `LanguageInfo` / `LanguageCapabilities` (UX-only change).

## Where the work lands

See [implementation-plan.md](implementation-plan.md) for the phased plan, the new
reusable component, the migration of the shared MRU into per-role lists, and the files to
touch.

## Verification

- `dotnet build Parlotype.slnx` clean (zero warnings); `dotnet test` green.
- Headless UI test: clicking the source button shows the source picker; clicking the target
  button swaps to the target picker; clicking the active button collapses the picker.
- Headless UI test: filtering narrows the list and re-shows `Recent` labels on remaining
  recents only.
- Headless UI test: toggling the arrow disables/enables the target button and updates
  `TranslationEnabled` in settings.
- Whisper engine: target=`en` ⇒ legacy `TranslateToEnglish` is set; effective
  `WhisperOptions.TranslateToEnglish == true` at pipeline cache time.
- Whisper engine: clicking the target button while Whisper is active offers only `English`.
- Gemma 4 engine: arbitrary target survives toggle-off → toggle-on (last value preserved).
- MRUs: source selections do not pollute the target MRU and vice versa; legacy
  `RecentLanguages` migrates into the source MRU on first load (one-time).
- ADR created (UI redesign + new `TranslationEnabled` key + MRU split) referencing ADR-021,
  ADR-033, ADR-034.
- Memory vault: `memory/services/Parlotype.Desktop.md` + `Parlotype.Core.md` updated; new ADR
  added to `memory/decisions/_index.md`.
