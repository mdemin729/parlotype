---
title: "Session: 2026-05-25 — Translation Model Capability"
type: session
status: complete
tags: [whisper, translation, adr, ui, tests, screenshot-tests]
created: 2026-05-25
summary: Implemented ADR-033 — gates Whisper translation by model capability, disabling the translate toggle for *En and LargeV3Turbo models while preserving the user's saved preference. Includes pipeline gate, model list hint, intent-aware note text with accent/muted styling, wiring tests, screenshot scenarios, and post-review naming cleanup.
---

# Session: 2026-05-25 — Translation Model Capability

## Active Focus

- `src/Parlotype.Core/Speech/WhisperModelInfo.cs` — added `SupportsTranslation` bool
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — `effectiveTranslate` gate
- `src/Parlotype.Desktop/ViewModels/WhisperModelDisplayItem.cs` — exposed `SupportsTranslation`
- `src/Parlotype.Desktop/Views/Settings/WhisperModelSettingsView.axaml` — "no translation" hint column
- `src/Parlotype.Desktop/ViewModels/Settings/WhisperOutputSettingsViewModel.cs` — `CanTranslate`, `UpdateTranslationAvailability`, `TranslationUnavailableNote`, `ShowTranslationPausedNote`, `ShowTranslationUnavailableNote`
- `src/Parlotype.Desktop/Views/Settings/WhisperOutputSettingsView.axaml` — disabled toggle + two-TextBlock accent/muted notes
- `src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs` — `OnWhisperModelPropertyChanged` wiring
- `src/Parlotype.Tests/WhisperModelInfoTests.cs` (new)
- `src/Parlotype.Tests/AudioPipelineTests.cs` — two new pipeline gate tests
- `src/Parlotype.Desktop.Tests/WhisperOutputSettingsViewModelTests.cs` (new, 9 tests)
- `src/Parlotype.Desktop.Tests/SettingsWindowViewModelTests.cs` — end-to-end wiring test
- `src/Parlotype.Desktop.Tests/SpeechSettingsScreenshotTests.cs` — 3 new WhisperOutput scenarios
- `docs/decisions/033-translation-model-capability.md` (new ADR)
- `memory/decisions/_index.md`, `memory/services/core.md`, `memory/services/desktop.md`
- `memory/knowledge/whisper-translation-models.md` — corrected (Turbo wrongly listed as capable)
- `plans/2026-05-25-translation-model-capability/` (new plan folder)

## Decisions Made

- **Single source of truth**: `WhisperModelInfo.SupportsTranslation` in Core — `false` for `TinyEn`, `BaseEn`, `SmallEn`, `MediumEn`, `LargeV3Turbo`; `true` for all remaining multilingual models.
- **Authoritative gate in pipeline**: `effectiveTranslate = intent && SupportsTranslation`. UI state can never bypass this. User's saved `TranslateToEnglish` preference is never overwritten.
- **Cross-VM wiring**: `SettingsWindowViewModel` subscribes to `WhisperModel.PropertyChanged` and calls `WhisperOutput.UpdateTranslationAvailability(...)` — mirrors the existing `OnSpeechEnginePropertyChanged` pattern.
- **Intent-aware note text**: two distinct wordings based on `TranslateToEnglishEnabled`:
  - Preference on → "Translation is paused — … resumes automatically when you pick a multilingual model."
  - Preference off → "The selected model doesn't support translation."
- **Accent styling for paused state**: two mutually exclusive TextBlocks driven by `ShowTranslationPausedNote` / `ShowTranslationUnavailableNote`. Paused note gets `Foreground="{DynamicResource SystemAccentColor}"` (same accent as model selection indicator); unavailable note stays `Opacity="0.6"`. No converter needed — compiled bindings only.
- **Naming convention**: view-driving booleans named `Show*Note` (not `Is*`) to make the binding intent explicit and avoid ambiguity with `!CanTranslate`.

## Facts Learned

- **LargeV3Turbo does NOT support translation** — it is a distilled, transcription-only model. Despite being multilingual for transcription, it was fine-tuned without the translate task. The previous `memory/knowledge/whisper-translation-models.md` wrongly listed it as translation-capable; corrected this session.
- **Avalonia compiled bindings (`x:CompileBindings="True"`) validate property paths at build time** — a missed rename in AXAML produces a build error, not a silent runtime failure. This makes compiled bindings a strong refactoring safety net.
- **`SystemAccentColor` is available as a `DynamicResource` in all Avalonia themes** — the same resource used for the model-list selection border. Reusing it for the "paused" note creates visual consistency ("accent = your active choice").
- **Avalonia `IsVisible="False"` hides a control but its Grid column retains its sizing** — when the "no translation" hint (column `*`) is hidden, the column still stretches, keeping DiskSize right-anchored. Intentional; a comment was recommended for future readers.
- **Two `UpdateTranslationAvailability` calls on construction** — `SettingsWindowViewModel` calls it synchronously for immediate UI state; `WhisperOutputSettingsViewModel.InitializeAsync` (fire-and-forget) calls it again after reading settings. Both converge on the same value; the redundancy is safe and intentional.

## Open Blockers

- `plans/2026-05-25-translation-model-capability/task.md` has one DoD checkbox unchecked (manual run verification). The verification was done empirically but the file was not updated. Low priority.

## Documentation Status

- ADR: done — `docs/decisions/033-translation-model-capability.md`
- Vault (services/architecture): done — `memory/services/core.md`, `memory/services/desktop.md`, `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/whisper-translation-models.md` corrected

## Next Action

Resume the **cloud / online speech providers** track (deferred from ADR-032 session). Starting point from the previous handoff note (`2026-05-25-1013-online-providers-positioning.md`):

1. Provider selection & comparison — research OpenAI Whisper API, Groq, Deepgram; pick first vertical slice; ADR.
2. Secure key storage — DPAPI vs Windows Credential Vault vs encrypted JSON; ADR + Core/Platform spike.
3. `ISpeechRecognizer` extension shape — confirm batch signature is sufficient or design `IStreamingSpeechRecognizer`.
4. Settings UI — provider picker + key entry + visible-indicator (transparency commitment).
5. Fallback behaviour — local fallback vs hard fail vs user prompt; ADR-worthy.

All must honour the 5 brand commitments in [[../../docs/decisions/032-online-speech-providers-positioning|ADR-032]].
