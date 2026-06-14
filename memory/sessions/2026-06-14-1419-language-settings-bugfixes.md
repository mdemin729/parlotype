---
title: "Session: 2026-06-14 — Language Settings bug fixes (3 fixes)"
type: session
status: active
tags: [language, translation, transcribe, settings, navigation, viewmodel]
created: 2026-06-14
summary: "Fixed three Language UX bugs: stale target label when translation off ('Same as source'), Transcribe strip showing 'Auto = Auto' at startup (relationship not initialized), and the strip's source row not deep-linking to the Language settings page. All on branch fix_language_seettings_bugs; 210 Desktop tests green. Changes uncommitted."
---

# Session: 2026-06-14 — Language Settings bug fixes

## Active Focus

Three independent bug fixes on branch `fix_language_seettings_bugs` (all in
`src/Parlotype.Desktop`, plus tests). **Working tree is uncommitted.**

1. **Stale target label when translation disabled** —
   `LanguageRelationshipViewModel.TargetDisplayLabel` showed the last-used target
   (e.g. "Russian") while translation was off, contradicting the summary line.
   Now returns **"Same as source"** when `!TranslationEnabled ||
   IsNoTranslation(TargetCode)`. Added `NotifyPropertyChangedFor(TargetDisplayLabel)`
   on `TranslationEnabled` and to `NotifyCapabilityDerived()`.

2. **Transcribe strip "Auto = Auto" at startup** — the shared singleton
   `LanguageRelationshipViewModel` was only initialized by
   `LanguageSelectionSettingsViewModel`'s ctor (Settings page). At startup the
   strip bound to an uninitialized relationship. `TranscribeViewModel` now kicks
   off `_relationship.InitializeAsync()` (fault-logged) in its ctor and exposes
   `public Task RelationshipInitialization` for deterministic test awaiting.

3. **Strip source row opened last-viewed Settings section** — added deep-linking:
   new `ViewModels/SettingsSection.cs` enum (`Language`),
   `IWindowManager.ShowSettings(SettingsSection? section = null)`,
   `SettingsWindowViewModel.NavigateTo(SettingsSection)`, and
   `TranscribeViewModel.GoToLanguageSettings` now passes `SettingsSection.Language`.

## Decisions Made

- Off-state target label: **"Same as source"** (over echoing the concrete source
  language) — stays correct when source is Auto-detect or System keyboard layout
  where there is no single concrete language. User-confirmed.
- Startup init keyed off `TranslationEnabled` (not just the "Off" sentinel),
  because the connector toggle **preserves** `TargetCode` for restore-on-reenable;
  keying off the sentinel alone missed the reported FR→RU→disable case.
- Init lives in `TranscribeViewModel`'s ctor mirroring the Settings VM pattern;
  `InitializeAsync` is idempotent (`_initialized` guard) so both surfaces calling
  it is safe — whichever is built first performs the one load.
- Deep-link modeled as a generic `SettingsSection?` param on `ShowSettings`
  (only `Language` for now) + a VM `NavigateTo` that maps enum→section and
  no-ops if the section isn't visible for the active engine.

## Facts Learned

- The Settings nav selection is preserved across engine switches via
  `SettingsWindowViewModel.RebuildNavItems()`; `NavigateTo` overrides it by
  setting `SelectedNavItem` to the matching `NavItems` row.
- `LanguageCatalog.GetDisplayLabel("ru")` returns "Russian — Русский" (English —
  Native when they differ), not just "Russian" — relevant when asserting labels.
- Test helpers (`TranscribeLanguageStripTests.CreateAsync`) pre-call
  `relationship.InitializeAsync()`, which is exactly why the startup bug escaped
  test coverage; the new regression test deliberately skips pre-init and awaits
  `vm.RelationshipInitialization`.

## Open Blockers

None. `dotnet build Parlotype.slnx -p:EnableCuda=false` is zero-warning;
`dotnet test src/Parlotype.Desktop.Tests` → 210/210 green. Recurring Windows
file-lock on `Parlotype.Platform.dll` from stray `.NET Host` processes — kill by
PID then rebuild (see [[../knowledge/_index]] env notes).

## Documentation Status

- ADR: none required by the strict triggers — changes are Desktop-only (no
  `Parlotype.Core` type, no `PlatformServiceExtensions` entry, no new csproj dep,
  no OS/flag-conditional behaviour, no P/Invoke, no audio/hotkey/settings/Whisper
  subsystem change). New public symbols are a Desktop enum (`SettingsSection`) and
  an `IWindowManager` signature change. **Offered ADR/vault update to user; left
  pending pending their call.**
- Vault (services/desktop.md): pending — `SettingsSection`, `NavigateTo`,
  `TranscribeViewModel.RelationshipInitialization`, `ShowSettings(section)` not
  yet listed.
- Knowledge: none required (facts above are derivable from code/tests).

## Next Action

Commit the three fixes (working tree currently dirty: 10 modified + 1 new file).
Then, if the user wants docs: add the new Desktop public symbols
(`SettingsSection`, `IWindowManager.ShowSettings(section)`, `NavigateTo`,
`RelationshipInitialization`, off-state "Same as source" label) to
`memory/services/desktop.md`, and consider whether the `IWindowManager`
deep-link warrants a short ADR.
