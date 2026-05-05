---
title: "Session: 2026-05-03 14:32 — settings reload and docs cleanup"
type: session
status: active
tags: [audio-pipeline, settings, translation, readme, memory-vault, agent-config]
created: 2026-05-03
summary: "Fixed speech settings not applying until model switch, added loading status, updated README, consolidated agent config, removed duplicate memory/skills/"
---

# Session: 2026-05-03 14:32 — settings reload and docs cleanup

## Active Focus
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — fixed recognizer init guard to always pass `WhisperOptions`
- `src/Parlotype.Desktop/ViewModels/Settings/SpeechSettingsViewModel.cs` — stop recording when speech settings change
- `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs` — show "Loading model..." status during pipeline init
- `src/Parlotype.Tests/AudioPipelineTests.cs` — added `SpySpeechRecognizer` + 2 reinitialization tests
- `src/Parlotype.Desktop.Tests/TranscribeViewModelTests.cs` — added loading status test with `StartDelay` mock
- `README.md` — added Platform Support and CUDA installation sections, fixed Desktop.V2 references
- `memory/skills/` — deleted (duplicated `.claude/skills/`)
- `.copilot/agents/` — moved to `.claude/agents/`, normalized `.agent.md` → `.md`
- `memory/AGENTS.md` — renamed to `memory/CLAUDE.md`
- Updated cross-references in `CLAUDE.md`, `vault-map.md`, `generate-index.sh`, `ADR-013`, `agent-skills.md`, 4 `SKILL.md` files

## Decisions Made
- **Always call `InitializeAsync(WhisperOptions)`** in `AudioPipelineService.StartAsync` instead of gating on `IsReady`. The recognizer's built-in record equality check handles the short-circuit when nothing changed.
- **Stop recording on any speech setting change** (wait time, punctuation, profanity, translate). User must press Start/hotkey to resume with new settings. Follows same pattern as `WhisperModelSettingsViewModel`.
- **Skip ADR** for the pipeline fix — straightforward bug fix, no new interfaces or dependencies.
- **Single canonical location for agent skills**: `.claude/skills/*/SKILL.md` only. Removed `memory/skills/` duplicates.
- **Single canonical location for agent definitions**: `.claude/agents/` only. Removed `.copilot/agents/`.
- **Rename `memory/AGENTS.md` → `memory/CLAUDE.md`** for consistency with Claude/Copilot CLI conventions.

## Facts Learned
- `AudioPipelineService.StartAsync` cached `WhisperOptions` from settings via `CacheSettingsAsync` but never passed them to the recognizer when `IsReady` was already true. The `WhisperSpeechRecognizer.InitializeAsync(WhisperOptions)` overload already handles option comparison via record equality — it just wasn't being called.
- Git remote was updated from `parlotype-prototype` to `parlotype` — no files referenced the old name.

## Open Blockers
- None.

## Documentation Status
- ADR: none required (bug fix + docs changes only)
- Vault (services/architecture): none required (no new public symbols)
- Knowledge (non-derivable facts): none new — pipeline behavior was a bug, not a quirk

## Next Action
No in-flight work. When resuming, start from the next user request.
