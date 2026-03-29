---
title: Architecture Overview
type: index
status: active
tags: [architecture, overview]
last_updated: 2026-03-28
summary: Navigation index for Parlotype architecture documentation
---

# Architecture

Parlotype is a 7-project .NET 10 solution using a layered architecture with strict dependency direction.

## Documents

| Document | Summary |
|----------|---------|
| [[audio-pipeline]] | End-to-end audio capture → VAD → transcription → text injection flow |
| [[dependency-graph]] | Project dependency directions and data flow |
| [[subsystems]] | Text injection, global hotkeys, settings, logging, model management |

## Related Decisions

See [[decisions/_index]] for ADRs explaining why each architectural choice was made.
