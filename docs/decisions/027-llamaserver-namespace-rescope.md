---
status: accepted
date: 2026-05-18
---

# 027. `LlamaServer` Namespace Rescope (Out of `Speech.*`)

## Context

ADR-025 introduced `LlamaCppSpeechRecognizer` as a `llama-server` consumer
for Gemma 4 transcription. ADR-026 added a full managed-install subsystem
(catalog, registry, installer, manifest, dialog wrapper) under
`Parlotype.Core.Speech.LlamaServer.*` and
`Parlotype.Platform.Speech.LlamaServer.*`. Placing those types under
`Speech.*` made sense at the time because the only consumer was the
speech recognizer.

A second `llama-server` consumer is now planned: **post-processing**
(translation, stylisation, grammar correction, summarisation) on a
non-Gemma local LLM. The pipeline becomes
`Whisper → text → llama-server-hosted LLM → text injector`, sharing the
same installed binary, settings, and lifecycle as the speech path.

The server-side components — catalog, installer, registry, manifest,
probe helper, dialog wrapper, lifecycle interface — are
**workload-agnostic**. Keeping them under `Speech.*` would force the
post-processor consumer to import `Parlotype.Core.Speech.LlamaServer` to
talk to a runtime that has no inherent speech relationship, and would
keep misrepresenting the scope in source navigation, IDE search, and
docs.

## Decision

Move the server-side `LlamaServer` subsystem out of `Speech.*` to a flat
top-level namespace alongside `Audio`, `Speech`, `Hotkeys`, `Settings`.
Mirror the move in Platform and in the test project. Pure refactor — no
API-shape changes, no schema changes, no settings-key changes.

### Namespace mapping

| Before | After |
|---|---|
| `Parlotype.Core.Speech.LlamaServer.*` | `Parlotype.Core.LlamaServer.*` |
| `Parlotype.Platform.Speech.LlamaServer.*` | `Parlotype.Platform.LlamaServer.*` |
| `Parlotype.Platform.Speech.LlamaCppServerInfo` (probe helper, server concern) | `Parlotype.Platform.LlamaServer.LlamaCppServerInfo` |
| `Parlotype.Tests.Speech.LlamaServer.*` | `Parlotype.Tests.LlamaServer.*` |

### What stays put

- **`LlamaCppSpeechRecognizer`** stays in `Parlotype.Platform.Speech`. It
  is a *speech consumer* of `LlamaServer`, not the server itself.
- **`Gemma4ModelInfo`** stays in `Parlotype.Core.Speech`. Gemma 4 is the
  speech model; a future text-only LLM for post-processing will get its
  own metadata in a separate namespace.
- **`SpeechEngine` enum** + **`SettingsKeys.SpeechEngine`** stay in
  `Parlotype.Core.Settings` / `Speech`. They select the *speech* engine.
- **`SettingsKeys.LlamaCpp*`** keys (`LlamaCppActiveInstall`,
  `LlamaCppServerFolder`, `LlamaCppPort`) are unchanged — these are
  stable, user-facing keys persisted in `settings.json`.
- **`StreamingFileDownloader`** stays in `Parlotype.Platform.Speech` —
  it was extracted there in ADR-026 phase 4 and is shared by the
  Whisper model downloader and the llama-server installer. A possible
  future move to `Parlotype.Platform` flat is out of scope here.
- **On-disk layout**: `%LOCALAPPDATA%\parlotype\llama-servers\` and the
  `manifest.json` schema are unchanged.

### Why flat, not `Runtime.*` or `Inference.*`

`llama.cpp` is a concrete external tool, not an abstract category. The
contracts inside (catalog, installer, registry, lifecycle) describe
*that server's lifecycle*, independent of whether the workload is ASR
or post-processing. Post-processing will get its own sibling namespace
(`Parlotype.Core.Postprocessing` or similar) and *consume*
`LlamaServer` — the same way `Speech` already does. Adding a `Runtime`
or `Inference` umbrella before a second runtime concretely exists would
be premature abstraction.

### Architecture-doc rename

`docs/architecture/llamacpp-integration.md` →
`docs/architecture/llamacpp-subsystem.md`, with its intro and §1
component overview restructured to split **Server-side** (workload-
agnostic) from **Consumers** (today: speech only). The §4 lifecycle
state machine and §12 install lifecycle are unchanged in content but
the surrounding text now clarifies that they describe the *server*
lifecycle, not a speech-specific one.

## Consequences

### Positive

- Honest namespace. Source navigation, IDE Go-To-Definition, and ADR
  references all reflect the actual scope.
- Post-processing consumer slots in cleanly without an apologetic
  `using Parlotype.Core.Speech.LlamaServer` at the top of every
  non-speech file.
- Documentation matches code. `llamacpp-subsystem.md` is the canonical
  reference for both consumers.
- Pure refactor — no behaviour, schema, or settings change. Backwards
  compatible for all existing installs (settings keys, manifest, and
  on-disk layout untouched).

### Negative

- 28 files moved; ~22 `using` sites updated. Anyone with an in-flight
  branch touching `Parlotype.*.Speech.LlamaServer.*` will hit a rebase
  conflict. The refactor was kept atomic per phase so rebasing is a
  mechanical s/Speech.LlamaServer/LlamaServer/g.
- Temporarily ahistorical link in [ADR-025](025-gemma4-llamacpp-desktop.md)
  text body — left as-is per ADR-immutability convention; readers
  reach the new doc via this ADR's "Related" section.

### Open follow-ups

1. **`LlamaServerHost` extraction.** `ILlamaCppServerLifecycle` is
   still implemented by `LlamaCppSpeechRecognizer`. With one consumer
   that is fine — the recognizer happens to own the process. With two
   consumers sharing the same `llama-server`, the lifecycle has to
   move to a dedicated process-owning class so neither consumer can
   tear it down on the other. **Trigger:** the first post-processor
   landing in the codebase. **Scope:** new
   `Parlotype.Platform.LlamaServer.LlamaServerHost` that owns
   `Process`, `_serverProcess`, `_initLock`, and the spawn/health-poll
   logic; recognizer and post-processor both consume it.
2. **`ILlamaCppServerProbe` promotion to Core.** The
   `LlamaCppSettingsViewModel` currently imports `LlamaCppServerInfo`
   directly from Platform — a Core/Platform boundary leak noted in the
   architecture doc's §11 *Observations*. The namespace move makes the
   violation slightly more visible but does not fix it. **Trigger:**
   any meaningful UI refactor of the llama settings page, or the
   `LlamaServerHost` extraction (whichever comes first).
3. **Promote `StreamingFileDownloader` out of `Speech`?** It is a
   generic HTTP-to-disk helper shared by the Whisper downloader and
   the llama installer. Living under `Parlotype.Platform.Speech` is a
   minor lie. **Trigger:** a third consumer, or a Speech-namespace
   audit.

## Alternatives Considered

1. **`Parlotype.Core.Runtime.LlamaServer`** — adds a generic "Runtime"
   umbrella anticipating other managed runtimes (Ollama, vLLM, …).
   Rejected: premature umbrella, no concrete second runtime planned.
2. **`Parlotype.Core.Inference.LlamaServer`** — imposes an "AI
   inference" category. Rejected: imposes a workload taxonomy on
   server-lifecycle code, which is actually workload-neutral.
3. **`Parlotype.Core.LlmRuntime.LlamaServer`** — narrower variant of
   the above. Same rejection.
4. **Leave under `Speech.*` and add a second `Postprocessing.LlamaServer`
   copy** — rejected: duplication.
5. **Extract `LlamaServerHost` now** — would double the size of this
   refactor and risks regressing the sidecar lifecycle without a
   post-processor to validate against. Deferred (see "Open follow-ups"
   above).

## Related

- ADR-025: Gemma 4 via llama.cpp Sidecar in Desktop (introduced the
  speech consumer; namespace move does not change its semantics)
- ADR-026: Managed llama.cpp Server Installation (introduced the
  catalog/installer/registry types being moved; design decisions are
  unchanged)
- `docs/architecture/llamacpp-subsystem.md` (renamed in this ADR;
  was `llamacpp-integration.md`)
- `plans/2026-05-18-llamaserver-namespace-rescope/` — the 6-phase
  refactor plan
