---
title: "Session: 2026-05-19 — Settings grouped navigation"
type: session
status: active
tags: [settings, ui, navigation, mvvm, refactor]
created: 2026-05-19
summary: "Reorganised Settings window into four categories (Audio / Speech engine / Input / Appearance) with engine-scoped section hiding."
---

# Session: 2026-05-19 — Settings grouped navigation

## Active Focus

Settings window UX refactor — flat 8-row list → grouped nav with non-selectable
group headers and engine-scoped section visibility. ADR-028.

Files added:
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsCategory.cs`
- `src/Parlotype.Desktop/ViewModels/Settings/SettingsNavItem.cs`
- `src/Parlotype.Desktop/ViewModels/Settings/SilenceTimeoutSettingsViewModel.cs`
- `src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs`
- `src/Parlotype.Desktop/Views/Settings/SilenceTimeoutSettingsView.axaml(.cs)`
- `src/Parlotype.Desktop/Views/Settings/WhisperOutputSettingsView.axaml(.cs)`

Files removed:
- `src/Parlotype.Desktop/ViewModels/Settings/SpeechSettingsViewModel.cs`
- `src/Parlotype.Desktop/Views/Settings/SpeechSettingsView.axaml(.cs)`

Files modified:
- `SettingsSectionViewModelBase` gained `Category` and `RestrictToEngine`
- All eight section VMs declare `Category`; Whisper-only ones (Whisper model,
  Whisper runtime, Whisper output) declare `RestrictToEngine = Whisper`;
  llama.cpp declares `RestrictToEngine = Gemma4`
- `SettingsWindowViewModel` rewritten — `NavItems`/`SelectedNavItem` replace
  `Sections`/`SelectedSection`; subscribes to `SpeechEngineSettingsViewModel.SelectedEngine`
- `SettingsWindow.axaml` — `ListBox` template now switches between header and
  section row by `IsHeader`
- `App.axaml.cs` DI — swapped `SpeechSettingsViewModel` for the two new VMs
- Tests updated; `SettingsWindowViewModelTests` rewritten with 7 cases

## Decisions Made

- Concept B (flat list with non-selectable group headers) over tree / tabs
- Strict engine-hiding (no dimmed rows) for the inactive engine
- "Whisper output" as a dedicated subsection rather than merged into the
  Whisper model page — name chosen to leave room for "Gemma 4 output" later
  rather than embedding the Whisper-only assumption in the type system
- `JsonSettingsService` keys not migrated; only the VM containers split

## Facts Learned

- Avalonia `ListBox.Styles` can apply a class conditionally via
  `Setter Property="Classes.foo" Value="{Binding Bar}"` — used to make header
  rows non-selectable/non-focusable.
- xUnit v3 analyzer rule `xUnit1051` requires `TestContext.Current.CancellationToken`
  on any awaited call accepting a `CancellationToken` inside a test.

## Open Blockers

None.

## Documentation Status

- ADR: done — `docs/decisions/028-settings-grouped-navigation.md`
- Vault (services/architecture): done — `memory/services/desktop.md` updated
- Knowledge (non-derivable facts): none required

## Next Action

Manual smoke test the new nav (CUDA build) — open Settings, switch engine
Whisper ↔ Gemma 4, verify the right rows appear/disappear and that the
selection is preserved when possible.
