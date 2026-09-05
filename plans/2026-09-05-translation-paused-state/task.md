---
title: Translation paused is a first-class UI state
status: completed
created: 2026-09-05
started: 2026-09-05
completed: 2026-09-05
---

# Translation paused is a first-class UI state

## Problem

Selecting a Whisper model that cannot translate (the four `*En` models and Large v3 Turbo)
left every language surface still showing a working translation. The Transcribe strip —
the surface a user watches while dictating — read `English → English` with the accent
connector and gave no signal at all; the Settings → Language page contradicted itself
(connector on, switch on, summary claiming an English output) with only one small accent
line below saying otherwise.

The capability data and the pipeline gate from [ADR-033](../../docs/decisions/033-translation-model-capability.md)
were intact — only its UI half was, having been dropped in the
[ADR-036](../../docs/decisions/036-language-ux-rebuild.md) language rebuild down to a
single boolean bound to one line of text. So the exact confusion ADR-033 was written to
end had returned: translation enabled, nothing translated, no visible cause.

## Approach

Make model-blocked translation a distinct **reversible** state — `ConnectorState.Paused` —
rendered identically on both surfaces, without ever overwriting the user's preference
(ADR-033's central rule). Rejected: making `SpeechEngineCapabilities` model-aware so the
engine collapses to `TranslationForm.None`, because `ApplyEngine` force-disables
translation on that form and would destroy the preserved intent.

Full rationale and the superseded ADR-033 UI points: [ADR-061](../../docs/decisions/061-translation-paused-state.md).

## Workplan

- [x] `ConnectorState.Paused` + `IsTranslationPaused` (replacing `ShowTranslationPausedNote`)
- [x] Truthful derived state: `SummaryText`, `ConnectorGlyph`, `ConnectorTooltip`,
      `TranscribeViewModel.TargetShort`
- [x] Name the cause: `WhisperModel` held in the relationship VM → `TranslationPausedNote`
- [x] Offer the cure: "Choose a model that translates" on the Language page banner and the
      Transcribe flyout, via a new `SettingsSectionViewModelBase.NavigationRequested`
- [x] Announce at the moment of the switch: toast from `SetWhisperModel`, silent at startup
- [x] Amber styling for the paused connector on both surfaces; warn palette promoted to
      `Application.Resources`
- [x] Whisper model list: "no translation" becomes a bordered badge with a tooltip
- [x] Tests: 9 new (paused connector/summary/toast/tooltip/preference-preserved/startup,
      strip mirroring, model-page routing) + screenshot scenarios in both themes
- [x] ADR-061; ADR-033 annotated as amended
- [x] Vault: `services/desktop.md`, `architecture/subsystems.md`, `decisions/_index.md`
