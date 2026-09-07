# Implementation plan

Detail for the six phases in [task.md](task.md). Each phase is sized to be one plan
folder, one branch, one PR.

> **Phases 1, 2 and batch 3a are built.** This document is kept as the plan of record, so
> the sections below still read as they were written. Where the build diverged, the
> authority is [ADR-064](../../docs/decisions/064-ui-localization-foundation.md) and
> [task.md](task.md). Two divergences worth knowing before reading on:
>
> - **No `{ReflectionBinding}` exemption was needed.** Phase 1 below prices one in as the
>   cost of live switching. Avalonia 12 turned out to expose
>   `CompiledBinding.Create<TIn, TOut>(expr, source)`, and giving each key its own
>   `LocalizedString` makes the expression a plain property access — so `{loc:Tr}` is a
>   compiled binding and CLAUDE.md's rule stands untouched. Ignore the indexer-binding
>   sketch and the exemption paragraph.
> - **No separate CI step.** The repo has no general workflow; `release.yml`'s existing
>   `dotnet test` gate covers all the checks, which is why they were also written as xUnit
>   facts rather than script-only.

---

## Phase 1 — Foundation

**Goal:** the interface language can be chosen and actually changes something, with
exactly one view migrated. Everything after this phase is repetition.

### Core

- `SettingsKeys.UiLanguage` — `"system"` (default when unset) or a culture name.
- `Parlotype.Core/Localization/SupportedUiLanguages.cs` — the single registry:
  `record UiLanguage(string CultureName, string EndonymName)`, listing `system`, `en`,
  `ru`, `es`. Adding German later is one row here plus one resx file. Both the settings
  dropdown and the parity test read this list, so they cannot drift.

### Desktop

- `Resources/Localizer.cs` — sealed singleton, `INotifyPropertyChanged`,
  `string this[string key]` reading from `ResourceManager` at `CurrentUICulture`;
  missing key returns the key name (keeps ADR-056's never-crash-on-stale-resx
  behaviour). `SetCulture(CultureInfo)` sets `CultureInfo.DefaultThreadCurrentUICulture`
  and raises `PropertyChanged(null)`, which refreshes every bound string at once.
- `Markup/TrExtension.cs` — `{loc:Tr Key}`. `ProvideValue` returns
  `new Binding($"[{key}]") { Source = Localizer.Instance, Mode = OneWay }`.
- `Services/UiLanguageService.cs` — resolves the setting to a culture at startup (before
  any window is constructed) and applies changes at runtime.
- `ViewModels/Settings/InterfaceLanguageSettingsViewModel.cs` +
  `Views/Settings/InterfaceLanguageSettingsView.axaml`, in `SettingsCategory.Appearance`,
  registered in `SettingsWindowViewModel._allSections`. Follows the
  `ThemeSettingsViewModel` shape (display-item array + `[RelayCommand]`) — the closest
  existing precedent, and the section it will sit next to.
- `scripts/gen-strings.ps1` — reads `Strings.resx`, writes `Strings.cs` with the same
  public shape as today so `StringsTests` keeps passing. Emits
  `string Format_X(params ...)` helpers for keys whose value contains `{0}`, so call
  sites get compile-time arity instead of a runtime `FormatException`.

### Decision: live switch vs restart required

| | Live (recommended) | Restart required |
|---|---|---|
| AXAML | `{loc:Tr Key}` to a binding | `{loc:Tr Key}` to a constant string |
| CLAUDE.md conflict | needs a scoped `{ReflectionBinding}` exemption in the ADR | none |
| Tray menu | needs explicit rebuild on change | free |
| UX | matches Theme, which already switches live | a second "Restart required" panel |

The markup is identical under both, so the decision is reversible — but flip-flopping
after Phase 3 means re-reviewing every view, so settle it in the ADR.

If live is chosen, spell the exemption out in ADR-064 and keep it narrow: one markup
extension, one source type, one indexer, with key names checked at build time through the
generated `Strings` accessor in tests. It is not a licence for `{ReflectionBinding}` on
DataContext bindings.

### Verification

- Language set to `ru` with no `Strings.ru.resx` present renders English, no exception.
- Pilot `ThemeSettingsView` shows a translated title once a throwaway `ru` resx holding
  only that one key is dropped in.
- Existing `ThemeSettingsScreenshotTests` still pass.
- ADR-064 — Core gains a new record and registry, Desktop gains a DI registration; both
  are Definition-of-Done ADR triggers on their own.

---

## Phase 2 — Guardrails

**Goal:** make it mechanically impossible to ship a UI change that leaves a locale
stale, whether the change comes from Claude or from a human.

### scripts/check-localization.ps1

Four checks as built (the fourth was added during 3a), each with a non-zero exit and a
precise message:

1. **Key parity.** Every key in `Strings.resx` exists in every `Strings.<culture>.resx`
   for the cultures listed in `SupportedUiLanguages`; no locale carries orphan keys.
2. **Placeholder parity.** The `{0}`/`{1}` set matches per key across locales. A
   translation that drops a placeholder throws `FormatException` at runtime, in
   production, in the one language nobody on the team reads.
3. **`{loc:Tr}` keys resolve.** A typo'd key neither fails the build nor throws — the
   window renders the raw key name. Added during 3a, when the first real volume of
   markup keys made it a live risk.
4. **Hardcoded-literal scan.** `.axaml` files must not carry `Text=` / `Content=` /
   `Header=` / `Watermark=` / `PlaceholderText=` / `ToolTip.Tip=` / `ToolTipText=` /
   `Title=` with a literal value. Counts live in `scripts/localization-baseline.json` and
   may shrink but never grow. Numeric character references (`&#x2715;`) are treated as
   glyphs — the `x` in them otherwise reads as a letter and counts a glyph as copy.

### LocalizationParityTests (Parlotype.Desktop.Tests)

The same three checks as xUnit facts, so `dotnet test` and CI fail too. The hook only
covers Claude sessions and the requirement is stronger than that. Plus one more fact:
`Strings.cs` is byte-identical to what `gen-strings.ps1` would produce.

### .claude/skills/localization/SKILL.md

The repo has no checked-in `.claude/settings.json` yet — only `settings.local.json` —
so this phase creates one.

Skill scope, triggered when adding or changing user-facing UI text:

- key naming (`Area_Component_Purpose`), where the neutral resx lives, where
  translations live
- the add-a-string recipe: neutral resx → `gen-strings.ps1` → every locale resx →
  `{loc:Tr}` in markup or `Strings.X` in the view model → run the check script
- composite formats: numbered placeholders only, never interpolation; a note that
  Russian moves word order and that a placeholder carrying a noun needs a case-agreement
  review, so `"{0} isn't a source in {1}"` may need rephrasing rather than translating
- what is deliberately not localized (the scope boundary in task.md)
- how to add a whole new language — points at `docs/localization.md` from Phase 6
- translation quality bar: hold the English register (terse, sentence case, no
  exclamation marks) and never translate identifiers that appear verbatim in the UI
  (`Parakeet TDT v3`, `Vulkan`, `settings.json`)

### Hooks in .claude/settings.json

```
PostToolUse  Edit|Write|MultiEdit on **/Strings*.resx, **/Views/**/*.axaml,
             **/ViewModels/**/*.cs
             -> run check-localization.ps1; a non-zero exit surfaces the missing
                keys as feedback so they get fixed in the same turn

Stop         -> run check-localization.ps1; exit 2 blocks the stop and reports the
                failing keys, so a session cannot end with a stale locale
```

`PostToolUse` alone is not enough — a session can end mid-edit. `Stop` alone is not
enough — the feedback arrives long after the context that would make the fix cheap.
Both.

### Also in this phase

- CI: run the script in the build workflow next to `dotnet test`.
- `CLAUDE.md`: a Localization section, and a seventh Definition-of-Done item — *UI text
  changes update every supported locale*.
- Memory vault: a Localization section in `memory/architecture/subsystems.md`;
  `Localizer` / `TrExtension` / `UiLanguageService` listed in
  `memory/services/parlotype-desktop.md`; ADR-064 in `memory/decisions/_index.md`.

---

## Phase 3 — String extraction

183 AXAML literals across 22 files at the start of the phase (measured, not estimated),
plus the view-model copy, in four batches that are each independently reviewable and
revertable. **3a is done**, leaving 171 across 19 files.
Per batch: literals to resx, regenerate `Strings.cs`, markup to `{loc:Tr}`, view models
to `Strings.X` / `Strings.Format_X(...)`, screenshot tests re-baselined, check script
green.

- **3a — Widget, tray, dialogs.** ✅ *Done 2026-09-05 — 38 keys; 12 AXAML literals cleared.* `TranscribeWindow.axaml`, `TranscribeViewModel`
  (~37 literals, including the `StatusText` machine — "Loading model...",
  "Recording..." — and the engine label map), `App.axaml` tray menu and tooltip,
  `ConfirmationDialog`, `ModelDownloadDialog`, `ModelDownloadViewModel`.
- **3b — Settings shell and simple sections.** ✅ *Done 2026-09-06 — 97 keys; 51 AXAML literals cleared.* `SettingsWindow.axaml`,
  `SettingsCategoryExtensions.GetDisplayName`, the 18 `override string Title`
  properties, and the Microphone, SilenceTimeout, Theme, Startup, Update, Data, Runtime
  and Hotkey sections.
- **3c — Speech engine sections.** ✅ *Done 2026-09-06 — 119 keys; 123 AXAML literals cleared.* SpeechEngine, WhisperModel, WhisperOutput, Parakeet,
  Gemma4, LlamaCpp (44 literals — the largest single view), Prompts, CloudProvider,
  `ApiKeyBox`.
- **3d — Language section.** ✅ *Done 2026-09-06 — 33 keys; phase 3 complete, baseline empty.* `LanguageSelectionSettingsView`, `LanguagePickerView`,
  `LanguageRowFactory`, and `LanguageRelationshipViewModel` — 30 literals, most of them
  interpolated sentences that assemble state descriptions
  (`"You speak {0} → Parlotype types {1}."`, the paused-translation copy from ADR-061,
  six `ShowToast` messages). The hardest batch, so it goes last; give every composite
  format a resx comment naming what each placeholder holds, so translators are not
  guessing from the key name.

**Watch for during extraction:** fixed `Width` / `MinWidth` on labels and buttons. Note
them as they turn up rather than discovering all of them at once in Phase 4.

---

## Phase 4 — Russian

- Translate every key into `Strings.ru.resx`; register `ru` in `SupportedUiLanguages`.
- Run the screenshot tests under `ru` — they already render every settings section — and
  read the images. Russian runs ~35 % longer than English, so clipped buttons and wrapped
  nav items are the expected outcome, not a surprise.
- Fix layout by making controls content-sized, not by shortening translations.
- Keep a terminology sheet in the plan folder: one fixed Russian term per concept
  (dictation, recognition, engine, model, hotkey, push-to-talk, tray), held consistent
  across ~450 strings. It becomes the reference for German and French.

## Phase 5 — Spanish

The same loop for `Strings.es.resx`. Around 20 % expansion, and Phase 4 will already have
absorbed most of the layout work, so this is mostly a translation pass. Neutral `es`
rather than `es-ES` / `es-MX` — the copy carries no region-specific vocabulary.

## Phase 6 — Polish and playbook

- **Pseudo-locale.** A generated `Strings.qps-ploc.resx` (`[Ŝéţţîñĝŝ !!!]`), built from
  the neutral file by script. Switching to it makes every unextracted literal visible at
  once and forces ~40 % expansion — cheaper than a native review for catching
  stragglers, and it is how the hardcoded-literal scan gets validated.
- **ICU speech-language names.** `LanguageCatalog` carries hand-written English names
  plus `CultureInfo` endonyms today. Add localized names through `CultureInfo.DisplayName`
  under `CurrentUICulture`, so the ~99-language picker translates itself and stays out of
  the resx entirely.
- **docs/localization.md.** The add-a-language recipe: one `SupportedUiLanguages` row,
  one resx file, run the check script, screenshot review, done. Written so that it is
  true — if it is not, the abstraction leaked, and that is a Phase 6 bug.
- A note on the website repo (`C:\projects\mdemin729\parlotype-website`) that the app UI
  ships in EN, RU and ES.
