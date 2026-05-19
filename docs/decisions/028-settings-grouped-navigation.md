---
status: accepted
date: 2026-05-19
---

# 028. Settings window grouped navigation

## Context

The Settings window listed eight sections in a flat `ListBox`: Speech Engine,
Microphone, Whisper Model, Runtime, llama.cpp, Hotkey, Speech, Theme.

Two problems compounded over time as the speech-engine surface grew:

1. **Whisper-related options were scattered.** Whisper Model, Runtime (which is
   Whisper-only — CUDA/Vulkan/CPU pinning for whisper.cpp), and the three
   Whisper-only toggles inside "Speech" (Punctuation, Profanity, Translation)
   sat in three non-adjacent rows of the list.
2. **The "Speech" section mixed scopes.** Silence Timeout is engine-agnostic
   (a VAD/pipeline setting), while the three toggles are output-shaping
   options implemented today only for Whisper.

Additionally, "Runtime" and "llama.cpp" carried no hint of which engine they
belonged to. With Gemma 4 added (ADR-025), the inactive engine's settings
remained visible and selectable, adding noise.

## Decision

Reorganize the settings navigation into four categories, with non-selectable
group headers, and hide sections that belong to an inactive engine.

**New nav structure (engine = Whisper):**

```
Audio
   Microphone
   Silence timeout
Speech engine
   Engine
   Whisper model
   Whisper runtime
   Whisper output
Input
   Hotkey
Appearance
   Theme
```

When the engine is Gemma 4, the four Whisper-restricted rows are replaced by
`llama.cpp server`.

**Mechanism.** `SettingsSectionViewModelBase` gains two abstract/virtual
properties:

- `SettingsCategory Category` — the group the section belongs to.
- `SpeechEngine? RestrictToEngine` — `null` means always visible; otherwise
  the section is shown only when that engine is the active one.

`SettingsWindowViewModel` projects sections through these filters into an
`ObservableCollection<SettingsNavItem>` consumed by the left `ListBox`. The
list is rebuilt when `SpeechEngineSettingsViewModel.SelectedEngine` changes.
Header rows are `IsHeader = true, Section = null` and the AXAML applies a
`navHeader` class that disables hit-testing and focus.

**Speech section split.** The old `SpeechSettingsViewModel` is replaced by
two narrower view models:

- `SilenceTimeoutSettingsViewModel` (category Audio, no engine restriction)
- `WhisperOutputSettingsViewModel` (category SpeechEngine, restricted to
  Whisper)

`JsonSettingsService` keys (`WaitTime`, `AutomaticPunctuation`,
`FilterProfanity`, `TranslateToEnglish`) are unchanged.

**Naming honesty.** The new section is called "Whisper output" rather than
e.g. "Whisper-specific output" to reflect that these options happen to be
Whisper-only today because of how they're implemented, not because they're
conceptually bound to Whisper. If Gemma 4 grows its own output toggles, a
sibling `Gemma4OutputSettingsViewModel` is added rather than renaming this
one.

## Consequences

**Easier**

- Whisper-related settings are visually adjacent, matching the user's mental
  model of "I'm configuring Whisper."
- Hiding the inactive engine's backend (`Runtime`/`Whisper model` for Gemma 4,
  `llama.cpp server` for Whisper) eliminates settings that have no effect.
- Adding a new section requires only declaring `Category` (and optionally
  `RestrictToEngine`) — no manual reordering in `SettingsWindowViewModel`.
- Adding a new top-level group means adding one enum value to
  `SettingsCategory` and one row to `SettingsCategoryExtensions.GetDisplayName`.

**Harder / trade-offs**

- The list-box `ItemTemplate` now switches between a header presentation and
  a section presentation in the same template. That logic costs a small
  amount of XAML readability vs. the previous single-line template.
- The previous `Sections`/`SelectedSection` API surface is replaced by
  `NavItems`/`SelectedNavItem` (plus a `SelectedSection` convenience
  property). Any external test that asserted on the flat list had to migrate.
- Settings for the inactive engine cannot be inspected without switching the
  engine first. We chose this over the dimmed/disabled alternative because
  Parlotype expects one engine at a time during normal use.

**Risks**

- If a section's `RestrictToEngine` does not match the active engine when the
  Settings window opens, the user may notice rows appear/disappear when they
  switch the engine. The selection-preservation logic in
  `SettingsWindowViewModel.RebuildNavItems` keeps the current section
  selected if it survives the filter; otherwise it falls back to the first
  available row.

## Verification

- `dotnet build Parlotype.slnx` — clean.
- `dotnet test` — 438 tests pass (Core 242, Desktop 101, Benchmark 95).
- New `SettingsWindowViewModelTests` covers: ordering with both engines,
  default selection, engine-switch preservation, engine-switch fallback when
  the previous section is hidden, llama.cpp probe on selection.
