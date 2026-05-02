---
title: Knowledge Base
type: index
status: active
last_updated: 2026-05-01
summary: Semantic memory — stable facts learned across sessions that are not derivable from code
---

# Knowledge Base

This directory stores **stable facts** learned across sessions — things that are not obvious from reading the code alone.

## How to Add Knowledge
1. Create a new `.md` file in this directory with YAML frontmatter
2. Include: `type: knowledge`, `tags`, `created`, `summary`
3. Add an entry to this index

## Entries

<!-- Add entries as facts are learned across sessions -->
<!-- Format: | [[filename]] | one-line summary | date | -->

| Fact | Summary | Learned |
|------|---------|---------|
| [[whisper-net-quirks]] | Whisper.net 1.9.0 NuGet `CudaHelper` differs from upstream master; `WhisperLogLevel` enum is inverted vs native ggml | 2026-04-28 |
| [[agent-skills]] | Claude/Copilot skills require `.claude/skills/<name>/SKILL.md`; description-triggered discovery does not reliably fire at session boundaries, so per-session protocols belong in CLAUDE.md | 2026-04-30 |
| [[avalonia-devtools]] | Classic `Avalonia.Diagnostics` retired in 12; replacement is `AvaloniaUI.DiagnosticsSupport` (in-app) + `AvaloniaUI.DeveloperTools` (`avdt` global tool); free Essentials tier needs portal signup. Build telemetry: `AvaloniaStatsTask` POSTs hashed build metadata to `av-build-tel-api-v1.avaloniaui.net`; Community tier cannot opt out; no runtime telemetry. Set `AVALONIA_TELEMETRY_OPTOUT=1` when upgrading to paid tier. | 2026-04-30 |
| [[asyncrelaycommand-flicker]] | CommunityToolkit.Mvvm `AsyncRelayCommand` disables all buttons sharing the command while executing; use sync `RelayCommand` + fire-and-forget for shared commands in `ItemsControl` | 2026-04-30 |
| [[benchmark-pipeline-recommendations]] | Optimal STT settings from 234+ config sweep: Medium/en/beam=1/temp=0.0/no-VAD for accuracy; BaseEn for speed. language="en" gives 2× speedup; higher beam sizes never help | 2026-05-01 |
| [[vad-silence-threshold-constraint]] | AudioPipelineService processes VAD in 500ms chunks; silence threshold must be ≥ 500ms or unprocessed audio is mistaken for silence and pipeline flushes mid-speech | 2026-05-01 |

## Distillation Rules
- Only store facts that are **not derivable** from reading current code or git history
- Include the "why" — reasoning, context, constraints
- Update or remove entries when they become stale
- Prefer specific, actionable facts over vague observations
