---
title: Rescope LlamaServer out of Speech.*
status: completed
created: 2026-05-18
started: 2026-05-18
completed: 2026-05-18
---

# Rescope `LlamaServer` Out of `Speech.*`

## Problem

ADR-025 + ADR-026 placed the managed `llama-server` subsystem under
`Parlotype.Core.Speech.LlamaServer.*` and
`Parlotype.Platform.Speech.LlamaServer.*` because the only consumer at the
time was [`LlamaCppSpeechRecognizer`](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs)
(Gemma 4 transcription).

A second consumer is now planned: **post-processing** (translation,
stylisation, grammar correction, summarisation) on local LLMs running on
the same `llama-server`. The transcribe path becomes
`Whisper → text → LlamaCpp-hosted model → text injector`.

The catalog, installer, registry, manifest, and dialog wrapper are all
workload-agnostic. Keeping them under `Speech.*` misrepresents their scope
and complicates the upcoming post-processor work.

## Goal

Move the namespace to a flat `Parlotype.Core.LlamaServer` /
`Parlotype.Platform.LlamaServer` and refresh the architecture doc + ADR
trail to reflect the broader scope. **No behaviour changes, no schema
changes, no settings-key changes.**

## Scope decisions (confirmed with user 2026-05-18)

- **New namespace:** flat `Parlotype.Core.LlamaServer` (no premature
  `Runtime` or `Inference` umbrella).
- **`ILlamaCppServerLifecycle` ownership unchanged** for now — extracting
  a dedicated `LlamaServerHost` is flagged in ADR-027 as a follow-up
  triggered when the first post-processor lands.
- **Architecture doc:** rename
  `docs/architecture/llamacpp-integration.md` → `llamacpp-subsystem.md`,
  rescope intro to "the llama-server we manage and its consumers (speech
  today, post-processing tomorrow)".
- **Test layout mirrors prod:** `src/Parlotype.Tests/Speech/LlamaServer/`
  → `src/Parlotype.Tests/LlamaServer/`.

## Out of scope

- Implementing any post-processor (lands in a follow-up plan that adds
  `Parlotype.Core.Postprocessing` and a `LlamaCppPostprocessor`).
- `LlamaServerHost` extraction (`ILlamaCppServerLifecycle` stays on the
  speech recognizer for now).
- Renaming `SettingsKeys.LlamaCpp*` — these are stable user-facing keys.
- Changing on-disk layout, manifest schema, or DTO field names.
- Renaming `LlamaCppSpeechRecognizer` — stays in
  `Parlotype.Platform.Speech` (it's a speech consumer of `LlamaServer`).

## Phased workplan

Each phase = one reviewable commit. Build clean + 429 tests green after
every phase. Detail in [implementation-plan.md](implementation-plan.md).

- [x] Phase 1 — Move Core namespace (`Parlotype.Core.Speech.LlamaServer` → `Parlotype.Core.LlamaServer`, 13 files + ~22 `using` sites)
- [x] Phase 2 — Move Platform namespace + relocate `LlamaCppServerInfo` into the new `LlamaServer` folder
- [x] Phase 3 — Move test folder to mirror prod
- [x] Phase 4 — Rename + rescope `docs/architecture/llamacpp-integration.md` → `llamacpp-subsystem.md`
- [x] Phase 5 — Write ADR-027 + refresh memory vault (services, decisions index, knowledge note links)
- [x] Phase 6 — Plan housekeeping (mark completed in INDEX) + final clean-build verification + Desktop smoke check

## Verification

After every phase: `dotnet build Parlotype.slnx -p:EnableCuda=false` clean
and the full test suite stays green at **429 passing** (no test count
changes — pure refactor).

End-of-plan manual smoke:

1. Full clean build (`obj/` + `bin/` deleted) succeeds.
2. `dotnet test` reports 429 passing, zero failures, zero skipped.
3. `dotnet run --project src/Parlotype.Desktop` opens; Settings →
   llama.cpp still shows the Active server panel, Installed list, Manual
   panel, and Available builds list exactly as committed in Phase 7 of
   the previous plan.
4. `git log --follow src/Parlotype.Core/LlamaServer/LlamaServerInstall.cs`
   shows file history all the way back through the rename and the
   creation in commit `31d829a`.
