---
title: Parakeet as default engine + capability-driven language UI visibility
status: completed
created: 2026-07-07
started: 2026-07-07
completed: 2026-07-07
---

# Parakeet default & language UI visibility

## Problem

Follow-up to [2026-07-06-parakeet-v3-engine](../2026-07-06-parakeet-v3-engine/):

1. Parakeet always auto-detects (no language-forcing parameter, no translation),
   yet the Language settings page offered a 25-language source picker the engine
   silently ignores, and the Transcribe widget carried a useless language strip.
2. Parakeet outperformed Whisper in real use (speed, accuracy, resource usage,
   CPU-only) and should be the out-of-box default.

Chosen resolution for the language page: **hide it entirely for Parakeet**
(rather than keeping a documentation-only page).

## Approach

Capability-driven visibility (ADR-042): `LanguageCapabilities` gains
`SupportsSourceSelection` (false for Parakeet) and derived `HasLanguageChoices`;
the Language page hides via a new `SettingsSectionViewModelBase.IsVisibleFor`
virtual, the Transcribe strip hides via `HasLanguageStrip`, and the fixed-size
widget compacts 118 → 88 px. Engine switches to a choice-less engine skip the
spec-§8 fallbacks so stored language preferences survive the round trip.

Default engine: all unset-setting fallbacks become Parakeet (factory, engine
settings VM, relationship VM); engine cards reorder Parakeet-first
("Recommended"); `ParakeetSpeechRecognizer` auto-downloads the model on first
use (parity with Whisper's silent download — a default engine must not error
into Settings).

## Workplan

- [x] Core: `LanguageCapabilities.SupportsSourceSelection` + `HasLanguageChoices`;
      Parakeet declares no source selection
- [x] Desktop: `IsVisibleFor(engine)` on section base; Language page override;
      `SettingsWindowViewModel` filters by it
- [x] Desktop: `HasLanguageStrip` respects `HasLanguageChoices`; TranscribeWindow
      compacts to 88 px without the strip; `ApplyEngine` skips fallbacks for
      choice-less engines (nothing persisted)
- [x] Default: Parakeet fallback in `SpeechRecognizerFactory`,
      `SpeechEngineSettingsViewModel`, `LanguageRelationshipViewModel`;
      Parakeet-first card order; enum docs
- [x] Platform: recognizer auto-download via optional
      `ParakeetModelDownloadService` dependency
- [x] Tests: 732 green — updated engine-order/default/nav/chrome tests; new
      coverage for capability flags, strip hide/reappear, window resize,
      preference-preserving round trip
- [x] ADR-042, vault updates (core/platform/desktop, subsystems, decisions
      index), CLAUDE.md overview
