---
title: Align Pipeline Settings with ADR-011 Benchmark Recommendations
status: planned
created: 2026-05-01
started:
completed:
---

# Align Pipeline Settings with ADR-011 Benchmark Recommendations

## Problem

ADR-011 documents a systematic benchmark sweep across 234+ configurations that identified optimal STT pipeline settings (Medium model, `language: "en"`, `beamSize: 1`, `temperature: 0.0`, no VAD for batch). However, neither V1 nor V2 desktop applications use these settings:

- Both apps call the no-args `InitializeAsync()` which hardcodes `language: "auto"` and skips `WhisperOptions` entirely
- Default model is `Base` (not `Medium`) — 45% higher WER on challenging speech
- Beam size, temperature, and language are not configurable via UI
- VAD cannot be toggled by the user

V1 and V2 share identical pipeline behaviour through the common Core/Platform layer.

## Approach

Three-phase implementation, each independently shippable:

1. **Phase A — Wire settings into pipeline**: Make `AudioPipelineService` use `WhisperOptions`-based init. Add settings keys for language, beam size, and temperature. Change default model to Medium.
2. **Phase B — Expose in V2 UI**: Add language selector, advanced Whisper controls, and VAD toggle to the settings flyout.
3. **Phase C — Documentation**: Write ADR for default model change, update memory vault.

## Workplan

### Phase A: Apply ADR-011 defaults

- [ ] Add `SettingsKeys` for `WhisperLanguage`, `WhisperBeamSize`, `WhisperTemperature`
- [ ] Refactor `AudioPipelineService` to build `WhisperOptions` from settings and call `InitializeAsync(WhisperOptions)`
- [ ] Change `WhisperOptions.Model` default from `Base` to `Medium` (or add first-run model selection)
- [ ] Verify beam=1 and temp=0.0 are applied at runtime (log output)

### Phase B: V2 UI settings

- [ ] Add language selector to settings (en, auto, other common languages)
- [ ] Add beam size / temperature controls (advanced/expandable section)
- [ ] Add VAD enable/disable toggle

### Phase C: Documentation

- [ ] Write ADR for changing default model from Base to Medium
- [ ] Update `memory/services/platform.md` and `memory/services/core.md` with new settings keys
- [ ] Update `memory/knowledge/` with benchmark pipeline recommendations
