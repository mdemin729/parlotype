---
title: "Session: Parakeet default + language UI visibility"
type: session
status: completed
tags: [parakeet, default-engine, language-ux, settings]
created: 2026-07-07
summary: "Made Parakeet the default engine and hid all language UI for it (Settings page + Transcribe strip, window compacts 118→88 px) — ADR-042."
---

# Session: Parakeet default + language UI visibility

## Active Focus

User follow-up on the Parakeet engine (same day as ADR-041): the language
picker misled users (Parakeet always auto-detects), and Parakeet proved better
than Whisper in runtime use. Implemented ADR-042 in
`plans/2026-07-07-parakeet-default-language-ux/`.

## Decisions Made

- **Hide the Language page for Parakeet** (user chose removal over a
  documentation-only page); same flag hides the Transcribe strip and compacts
  the widget 118 → 88 px (fixed-size window — height switched in code-behind
  on `HasLanguageStrip` changes)
- **Capability-driven, not engine-hardcoded**: `SupportsSourceSelection` +
  `HasLanguageChoices` on `LanguageCapabilities`; new
  `SettingsSectionViewModelBase.IsVisibleFor(engine)` virtual (subsumes
  `RestrictToEngine`; prefer it for new sections)
- **Preserve preferences across engine round trips**: `ApplyEngine` skips
  spec-§8 fallbacks (and persists nothing) for choice-less engines — unlike
  the None-form fallback which flips `TranslationEnabled` off
- **Parakeet default**: unset-setting fallbacks only (explicit
  `SpeechEngine=Whisper` untouched); Parakeet-first "Recommended" card;
  recognizer silently auto-downloads the ~670 MB model on first use (parity
  with Whisper's silent download — default engine must not error into Settings)

## Facts Learned

- `TranscribeViewModel`'s ctor fire-and-forget kicks
  `LanguageRelationshipViewModel.InitializeAsync`, which re-reads the engine
  from settings and overrides any prior `SetEngine` — tests must seed
  `SettingsKeys.SpeechEngine` instead of calling `SetEngine` before VM
  construction (bit me in `Window_ResizesWithLanguageStrip_PerEngine`)

## Open Blockers

None. 732 tests green, zero warnings.

## Documentation Status

- ADR: done — `docs/decisions/042-parakeet-default-language-ux.md`
- Vault: done — core/platform/desktop profiles, subsystems Speech Engines
  table (Parakeet marked default), decisions index, memory/CLAUDE.md intro
- Knowledge: none needed (the InitializeAsync gotcha is code-derivable; noted
  here only)

## Next Action

Manual smoke test in the running app remains outstanding from ADR-041: fresh
default now exercises Parakeet — verify first record press triggers the silent
download + spinner, dictation injects text, and the widget shows compact (88 px,
no strip) with the Language page absent until switching to Whisper.
