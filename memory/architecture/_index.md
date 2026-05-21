---
title: Architecture Overview
type: index
status: active
tags: [architecture, overview]
last_updated: 2026-05-21
summary: Navigation index for Parlotype architecture documentation
---

# Architecture

Parlotype is an 8-project .NET 10 solution using a layered architecture with strict dependency direction (`Desktop / Benchmark → Platform → Core`; `Parlotype.Gemma4` peers with Platform).

## Documents

| Document | Summary |
|----------|---------|
| [[audio-pipeline]] | End-to-end audio capture → VAD → transcription → text injection flow (covers both Whisper and Gemma 4 / llama.cpp engines) |
| [[dependency-graph]] | Project dependency directions, external package usage, and Gemma 4 / llama.cpp boundaries |
| [[subsystems]] | Text injection, global hotkeys, settings, logging, model management, LlamaServer catalog/registry/installer |

## External Architecture Docs

Deeper subsystem documentation lives under `docs/architecture/`:

- `docs/architecture/llamacpp-subsystem.md` — managed llama-server install (ADR-026 + namespace rescope in ADR-027)

## Related Decisions

See [[decisions/_index]] for ADRs explaining why each architectural choice was made.

