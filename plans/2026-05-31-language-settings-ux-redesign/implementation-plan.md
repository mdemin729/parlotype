# Implementation Plan — Language Settings UX Redesign

Phased plan, each phase independently buildable/testable. Phases 0–1 are data/model
groundwork; Phases 2–3 deliver the new UI; Phases 4–5 close out tests, docs and ADR.

## Current state (grounding facts)

- **Language page:** [LanguageSelectionSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/LanguageSelectionSettingsView.axaml)
  stacks two separate `TextBox` + `ItemsControl` pickers vertically.
- **Source/target VM:** [LanguageSelectionSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/LanguageSelectionSettingsViewModel.cs)
  owns `SourceLanguages`/`TargetLanguages`, `SourceFilter`/`TargetFilter`,
  `SelectedSourceCode`/`SelectedTargetCode`, `ShowTargetPicker`,
  `ShowWhisperTranslationHint`. Both pickers share `_recent` (one MRU list).
- **English-translate toggle:** lives in
  [WhisperOutputSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs)
  / [WhisperOutputSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/WhisperOutputSettingsView.axaml)
  as `TranslateToEnglishEnabled`. Gated by `WhisperModelInfo.SupportsTranslation` (ADR-033).
- **Pipeline wiring:** `AudioPipelineService.CacheSettingsAsync` reads
  `SettingsKeys.TranslateToEnglish` into `WhisperOptions.TranslateToEnglish` and
  `SettingsKeys.SelectedSourceLanguage` into `WhisperOptions.Language`. Gemma 4 reads
  source/target through `LlamaCppSpeechRecognizer.BuildPromptTextAsync`.
- **Settings keys:** `TranslateToEnglish`, `SelectedSourceLanguage`,
  `SelectedTargetLanguage` (default `"none"`), `RecentLanguages` (shared).
- **Display item:** [LanguageDisplayItem.cs](../../src/Parlotype.Desktop/ViewModels/LanguageDisplayItem.cs)
  with `Code`, `DisplayName`, `IsRecent`, `IsSelected`, `SelectCommand`.

---

## Phase 0 — Settings model (Core)

Goal: introduce a first-class `TranslationEnabled` setting and split the shared MRU into
two per-role lists. No UI changes yet.

- **`SettingsKeys`** (`src/Parlotype.Core/Settings/SettingsKeys.cs`):
  - Add `TranslationEnabled = "TranslationEnabled"` (bool, default `false`).
  - Add `RecentSourceLanguages = "RecentSourceLanguages"` and
    `RecentTargetLanguages = "RecentTargetLanguages"`.
  - Mark `RecentLanguages` as **legacy** in XML doc (kept for one-shot migration).
  - Keep `TranslateToEnglish` in place — Phase 1 will derive it; Phase 4 may retire it once
    `AudioPipelineService` reads `TranslationEnabled` + `SelectedTargetLanguage` directly.

- **No change** to `LanguageCatalog`, `LanguageCapabilities`, `RecentLanguages` (helper).

- **Tests** (`Parlotype.Tests`): no new tests in this phase (settings keys are constants).
  The `RecentLanguages` helper is already covered.

> **ADR trigger:** new persisted keys + behaviour split → ADR required (see Phase 5).

---

## Phase 1 — Pipeline wiring update (Platform)

Goal: make the pipeline honour `TranslationEnabled` + `SelectedTargetLanguage` as the
source of truth, with backward-compat fallback to the legacy `TranslateToEnglish` flag.

- **`AudioPipelineService.CacheSettingsAsync`** (`src/Parlotype.Platform/Audio/AudioPipelineService.cs`):
  - Read `TranslationEnabled` (bool) and `SelectedTargetLanguage`.
  - For Whisper: compute `TranslateToEnglish =
    TranslationEnabled && SelectedTargetLanguage == "en"`. If `TranslationEnabled` key is
    absent (existing installations), fall back to the legacy `TranslateToEnglish` boolean —
    one-shot — and mirror it into the new keys.
  - For Gemma 4: pass `SelectedTargetLanguage` only when `TranslationEnabled` is true;
    otherwise pass `none` (no translation instruction added to the prompt).
  - Preserve the existing model-capability gate (`SupportsTranslation`, ADR-033): even with
    `TranslationEnabled == true`, the Whisper translation is skipped when the model can't
    translate.

- **One-time migration helper** — small private method `MigrateLegacyTranslationAsync`
  called from `InitializeAsync`-style entry that:
  - Sets `TranslationEnabled` from legacy `TranslateToEnglish` if `TranslationEnabled` is
    absent.
  - Sets `SelectedTargetLanguage = "en"` if `TranslationEnabled` is `true` and target was
    unset / `none`.
  - Copies legacy `RecentLanguages` into `RecentSourceLanguages` if the latter is empty.
  - Writes the migrated values back; the next launch is the new model.

  > Migration can live either in `AudioPipelineService` (settings-driven) or be triggered
  > from the desktop bootstrap; pick the smallest seam during implementation. The vault's
  > `memory/conventions/` should not need changes — this is a one-shot.

- **Tests** (`Parlotype.Tests/AudioPipelineTests.cs`):
  - When `TranslationEnabled = false`, `WhisperOptions.TranslateToEnglish` is false even
    with target = `en`.
  - When `TranslationEnabled = true` and target = `en`, `TranslateToEnglish` is true.
  - When `TranslationEnabled = true` and target = `fr` and engine = Whisper, the option is
    *not* set to true (Whisper can't translate to French; target is effectively ignored).
  - Migration: legacy `TranslateToEnglish = true` & no `TranslationEnabled` ⇒ on next read,
    `TranslationEnabled = true`, `SelectedTargetLanguage = "en"`.

---

## Phase 2 — Reusable `LanguagePickerView` component (Desktop)

Goal: extract the search + list pattern into a single reusable UserControl with its own
small ViewModel, so source and target reuse the same UI.

- **New ViewModel:** `src/Parlotype.Desktop/ViewModels/LanguagePickerViewModel.cs`
  - Constructor inputs:
    - `Header` (string): "Select source language" / "Select target language".
    - `IReadOnlyList<LanguageInfo>` *full* list (engine-aware; passed from parent).
    - Optional **leading sentinel** row (`Auto-detect` or null for target).
    - `Func<IReadOnlyList<string>>` *get recents* (callback to parent so MRU stays as a
      single source of truth even when there are two picker VMs).
    - `Action<string>` *on-select* (callback to parent).
    - `Func<string?>` *get selected code* (parent owns the persisted value).
  - Observable state: `Filter` (string), `Items`
    (`ObservableCollection<LanguageDisplayItem>`), `HasNoResults` (bool).
  - `Refresh()` method: rebuilds `Items` from current recents + full list + filter. Marks
    `IsSelected` from current selected code.
  - Internals reuse the existing `LanguageDisplayItem`, the existing `Matches` predicate,
    and the existing `Label` formatter from `LanguageSelectionSettingsViewModel`. Extract
    them to a `LanguageRowFactory` static helper to keep the new VM small.

- **New View:** `src/Parlotype.Desktop/Views/Settings/LanguagePickerView.axaml`
  - `x:CompileBindings="True"`, `x:DataType="vm:LanguagePickerViewModel"`.
  - Layout matches the prototype:
    - `TextBlock` heading (`{Binding Header}`).
    - `TextBox` bound to `Filter`, placeholder `Search languages…`.
    - "No languages match." block bound to `HasNoResults`.
    - `ItemsControl` of `LanguageDisplayItem` rows; row template is the existing
      `Grid ColumnDefinitions="11,*,Auto"` with accent bar + name + `Recent` label.
  - Code-behind only if needed for layout helpers (prefer fully-MVVM).

- **Tests** (`Parlotype.Desktop.Tests`): no headless test specific to the component yet
  (it's covered indirectly via the redesigned section in Phase 3).

---

## Phase 3 — Redesigned `LanguageSelectionSettingsView` (Desktop)

Goal: top-row source/target buttons with an arrow toggle between them, inline picker
below, engine-aware behaviour preserved.

### ViewModel changes

In `LanguageSelectionSettingsViewModel.cs`:

- **New observable state:**
  - `OpenPicker` (enum: `None | Source | Target`) — drives which picker is rendered and
    which button has the focus border.
  - `TranslationEnabled` (bool, `[ObservableProperty]`) — backed by the new settings key.
  - `IsTargetButtonEnabled` (computed) — `TranslationEnabled && _capabilities.SupportsArbitraryTranslation`
    is the Gemma 4 case; for Whisper it's `TranslationEnabled && SupportsAutoDetect == false ? false : true`
    (Whisper offers only English as target, so the button is still enabled when toggle is on).
    Final formula resolves to: **`TranslationEnabled` && there is at least one target option
    other than "none"**.
  - `IsArrowEnabled` — `_capabilities.SupportsArbitraryTranslation || _capabilities == Whisper-with-translation-capable-model`.
    Drives the visual greying of the arrow.
  - `SourceButtonLabel` / `TargetButtonLabel` (computed) — pretty label for the currently
    selected code (e.g. `"English"`, `"Russian — Русский"`). When target is `none`, fall
    back to a placeholder like `"English"` (the default that *would* be used) but render
    greyed to mirror the prototype's third frame.

- **New commands / methods:**
  - `[RelayCommand] OpenSourcePicker` — sets `OpenPicker = Source` (or toggles back to
    `None` if already open). Triggers `_sourcePickerVm.Refresh()`.
  - `[RelayCommand] OpenTargetPicker` — same for target; no-op while
    `TranslationEnabled == false`.
  - `[RelayCommand] ToggleTranslation` — flips `TranslationEnabled`. On enable, if
    `SelectedTargetCode` is `none`, default it to:
    - `en` for Whisper, or
    - last MRU target / `en` for Gemma 4.
    On disable, leave `SelectedTargetCode` untouched (so it is restored on re-enable);
    only the pipeline interpretation changes.

- **Child VMs:** the section now composes two `LanguagePickerViewModel` instances
  (`SourcePickerVm`, `TargetPickerVm`). The section keeps owning persistence + MRU; the
  child VMs just render. Wire callbacks:
  - `SourcePickerVm`: `getRecents = () => _sourceRecent`, `onSelect = SelectSource`,
    `getSelected = () => SelectedSourceCode`, leading sentinel = `Auto-detect`.
  - `TargetPickerVm`: `getRecents = () => _targetRecent`, `onSelect = SelectTarget`,
    `getSelected = () => SelectedTargetCode`, leading sentinel = `Default (no translation)`
    **only when the Gemma 4 capability is active**; for Whisper, no leading sentinel.

- **MRU split:**
  - Replace `_recent` with `_sourceRecent` and `_targetRecent`.
  - `SelectSource` promotes into `_sourceRecent` and persists
    `SettingsKeys.RecentSourceLanguages`.
  - `SelectTarget` promotes into `_targetRecent` and persists
    `SettingsKeys.RecentTargetLanguages`.
  - On `InitializeAsync`, read both. If `RecentSourceLanguages` is empty, fall back to the
    legacy `RecentLanguages` (one-shot migration; see Phase 1).

- **Engine awareness:** `UpdateForEngine` keeps its current job; additionally:
  - Recomputes `IsArrowEnabled` and `IsTargetButtonEnabled`.
  - For Whisper: rebuild target picker with the English-only set (already in
    `_capabilities.EffectiveTargetLanguages` if you wire it; otherwise compute inline).
  - Closes the target picker if `TranslationEnabled` is false or the active engine doesn't
    support arbitrary translation in a way that would invalidate the open state.

- **Selecting a language closes the picker:** `SelectSource` / `SelectTarget` set
  `OpenPicker = None` at the end (mirrors the prototype: list collapses after selection).

### View changes

Rewrite `LanguageSelectionSettingsView.axaml`:

```
Grid rows: [HeaderRow, PickerRow]
  HeaderRow:
    StackPanel Horizontal
      - Source side
        TextBlock "Source language"
        Button SourceButton  (Classes.active when OpenPicker == Source)
          Content = SourceButtonLabel
          Command = OpenSourcePickerCommand
      - Arrow toggle
        Button ArrowButton  (Classes.disabled when !TranslationEnabled)
          Content = "→"
          Command = ToggleTranslationCommand
          IsEnabled = IsArrowEnabled
      - Target side
        TextBlock "Target language"
        Button TargetButton  (Classes.active when OpenPicker == Target,
                              Classes.disabled when !TranslationEnabled)
          Content = TargetButtonLabel
          Command = OpenTargetPickerCommand
          IsEnabled = IsTargetButtonEnabled

  PickerRow:
    ContentControl
      - When OpenPicker == Source: render LanguagePickerView with SourcePickerVm
      - When OpenPicker == Target: render LanguagePickerView with TargetPickerVm
      - When None: collapsed (IsVisible=False)
```

- Use `Classes.active="{Binding IsSourcePickerOpen}"` etc. and `<UserControl.Styles>` to
  paint the green focus border on the active button (mirrors the existing
  `Classes.recording` pattern on the microphone button, per `CLAUDE.md`).
- The disabled-button greying for the arrow + target can use a style on
  `Classes.disabled` or simply rely on Avalonia's default disabled visual; pick during
  implementation.
- `x:CompileBindings="True"`, `x:DataType` on the root, no `ReflectionBinding`.

### Whisper-output cleanup

- **Remove** the `Translate to English` block from
  `Views/Settings/WhisperOutputSettingsView.axaml` (the `<!-- Translate to English -->`
  `StackPanel`).
- **Remove** `TranslateToEnglishEnabled`, `CanTranslate`, `TranslationUnavailableNote`,
  `ShowTranslationPausedNote`, `ShowTranslationUnavailableNote`,
  `UpdateTranslationAvailability` from `WhisperOutputSettingsViewModel.cs` (and the related
  `OnTranslateToEnglishEnabledChanged`).
- **Move the "paused / unavailable" hint logic** to the Language page near the target
  button, when (engine == Whisper && target == `en` && `!model.SupportsTranslation`).
  Reuse the same wording. Bind to a new computed property on
  `LanguageSelectionSettingsViewModel`.
- Drop the call site that previously notified the Whisper-output VM of model changes
  (search for `UpdateTranslationAvailability`) and route it to the Language section VM
  instead so the new hint can react.

### Tests

`Parlotype.Desktop.Tests`:

- Existing `LanguageSelectionSettingsViewModelTests` extended:
  - Opening source picker sets `OpenPicker == Source`; clicking again toggles to `None`.
  - Opening target picker while `TranslationEnabled == false` is a no-op.
  - `ToggleTranslation` flips the bool and defaults the target to `en` (Whisper) or last
    MRU target (Gemma 4) on first enable.
  - MRU split: a source selection touches `RecentSourceLanguages` but not
    `RecentTargetLanguages`.
- `WhisperOutputSettingsViewModelTests` shrinks — the translate-related tests move to the
  Language section's tests.
- Update `SpeechSettingsScreenshotTests.cs` baselines: new Language section layout, removed
  Whisper-output toggle. Regenerate snapshots after first review.

---

## Phase 4 — Settings persistence cleanup

Goal: once the new path is in place and the migration has run, retire the redundant legacy
keys.

- **Keep** `TranslateToEnglish` key for *reading* during the one-shot migration in Phase 1,
  but no part of the runtime should write it any more.
- **Keep** `RecentLanguages` for the same reason (read once, then ignored).
- Add a short note in `SettingsKeys.cs` XML doc that these are legacy / migrated.
- A future cleanup PR may delete them entirely after a release window. **Out of scope for
  this plan.**

---

## Phase 5 — ADR + docs + memory vault

Goal: capture the UX redesign as an ADR; update the memory vault and INDEX.

- **ADR** (`docs/decisions/NNN-language-settings-ux-redesign.md`):
  - Context: ADR-021 / ADR-033 / ADR-034 set the data model and engine behaviour. The UI
    was split across Language + Whisper-output. This ADR consolidates the user-facing
    translation control under the Language page.
  - Decision: arrow-as-toggle row + inline shared picker; new
    `TranslationEnabled` key; split MRU.
  - Consequences: one-shot migration of legacy `TranslateToEnglish` and
    `RecentLanguages`; the Whisper-output section loses its translate toggle.
  - References: ADR-021, ADR-028 (grouped settings nav), ADR-033, ADR-034.

- **Memory vault** (`memory/`):
  - Update `memory/services/Parlotype.Desktop.md` to reflect the new
    `LanguagePickerView{Model}` component and the redesigned `LanguageSelectionSettings*`.
  - Update `memory/services/Parlotype.Core.md` for the new settings keys
    (`TranslationEnabled`, `RecentSourceLanguages`, `RecentTargetLanguages`).
  - Add the ADR row to `memory/decisions/_index.md`.
  - If `memory/architecture/subsystems.md` contains a "translation" or "language"
    subsection, refresh it.

- **`plans/INDEX.md`:** move this plan from Planned → In Progress (when started), then
  remove on completion (per `plans/WORKFLOW.md`).

---

## Critical files

- View / VM (rewritten):
  - [LanguageSelectionSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/LanguageSelectionSettingsView.axaml)
  - [LanguageSelectionSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/LanguageSelectionSettingsViewModel.cs)
- View / VM (trimmed):
  - [WhisperOutputSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/WhisperOutputSettingsView.axaml)
  - [WhisperOutputSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs)
- New shared component:
  - `src/Parlotype.Desktop/ViewModels/LanguagePickerViewModel.cs`
  - `src/Parlotype.Desktop/Views/Settings/LanguagePickerView.axaml`
- Core / Platform:
  - [SettingsKeys.cs](../../src/Parlotype.Core/Settings/SettingsKeys.cs) — new keys, legacy
    markers.
  - [AudioPipelineService.cs](../../src/Parlotype.Platform/Audio/AudioPipelineService.cs) —
    read `TranslationEnabled` + `SelectedTargetLanguage`, migrate from legacy.
- Tests:
  - [LanguageSelectionSettingsViewModelTests.cs](../../src/Parlotype.Desktop.Tests/LanguageSelectionSettingsViewModelTests.cs)
  - [WhisperOutputSettingsViewModelTests.cs](../../src/Parlotype.Desktop.Tests/WhisperOutputSettingsViewModelTests.cs)
  - [AudioPipelineTests.cs](../../src/Parlotype.Tests/AudioPipelineTests.cs)
  - [SpeechSettingsScreenshotTests.cs](../../src/Parlotype.Desktop.Tests/SpeechSettingsScreenshotTests.cs) — regenerate baselines.
- ADRs referenced: 021, 028, 033, 034.

## Open questions / risks

- **Arrow visual:** the prototype renders the arrow as a separate button. Decide during
  implementation whether to use a `Path` icon, a `TextBlock "→"`, or a `PathIcon` from
  Fluent. Keep it accessible (`AutomationProperties.Name = "Toggle translation"`).
- **Migration boundary:** putting the one-shot migration in `AudioPipelineService` couples
  it to the audio path. An alternative is a tiny `SettingsMigrator` invoked from
  `PlatformServiceExtensions` during DI bootstrap. Pick the smaller change set at
  implementation time and document it in the ADR.
- **Whisper target = English-only:** double-check whether to render a target picker at all
  for Whisper (since there's only one choice), or to hide the target side entirely and
  rely solely on the arrow toggle. The prototype's Whisper frame still shows the target
  button labelled `English`, so render it for parity.
