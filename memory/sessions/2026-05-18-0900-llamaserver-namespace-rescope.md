---
title: "Session: 2026-05-18 — LlamaServer namespace rescope"
type: session
status: active
tags: [llama-server, refactor, namespaces, adr-027, post-processing-roadmap]
created: 2026-05-18
summary: "Pure refactor: moved LlamaServer subsystem out of Speech.* in anticipation of a second consumer (post-processing). New ADR-027, doc rename, memory-vault refresh. 429 tests unchanged."
---

# Session: 2026-05-18 — LlamaServer namespace rescope

## Active Focus

Started the day finishing up the managed llama-server installer plan
(Phase 8 — ADR-026, architecture-doc updates, memory-vault refresh).
That commit is `db107bf`.

The user then signalled a planned post-processing feature:
`Whisper → text → llama.cpp-hosted local LLM → text injector` for
translation / stylisation / grammar / summarisation. The newly-landed
`Parlotype.Core.Speech.LlamaServer.*` namespace would misrepresent
scope once a non-speech consumer arrives.

A new plan was written
([plans/2026-05-18-llamaserver-namespace-rescope](../../plans/2026-05-18-llamaserver-namespace-rescope))
and executed in full in this session:

1. **Phase 1** — Moved `src/Parlotype.Core/Speech/LlamaServer/` (13 files)
   to `src/Parlotype.Core/LlamaServer/`; namespace
   `Parlotype.Core.Speech.LlamaServer` → `Parlotype.Core.LlamaServer`.
   Updated ~22 consumer `using` sites via PowerShell batch replace.
2. **Phase 2** — Moved `src/Parlotype.Platform/Speech/LlamaServer/`
   (5 files) → `src/Parlotype.Platform/LlamaServer/`. Relocated
   `LlamaCppServerInfo.cs` from `Parlotype.Platform.Speech` →
   `Parlotype.Platform.LlamaServer` (it's a server probe helper, not
   a speech concern). Two parent-namespace implicit-lookup spots
   needed explicit `using`s afterwards: `LlamaServerInstaller` for
   `StreamingFileDownloader`, and `LlamaCppSpeechRecognizer` for
   `LlamaCppServerInfo`. Also two fully-qualified
   `Platform.Speech.LlamaServer.LlamaServerInstaller.BuildInstallId(...)`
   call sites in the VM collapsed to the short form.
3. **Phase 3** — Moved `src/Parlotype.Tests/Speech/LlamaServer/`
   (6 files) → `src/Parlotype.Tests/LlamaServer/`; namespace
   updated to mirror.
4. **Phase 4** — `docs/architecture/llamacpp-integration.md` →
   `llamacpp-subsystem.md`. Intro rescoped to "the managed
   `llama-server` subsystem and its consumers" (speech today,
   post-processing tomorrow). §1 Component Overview split into
   **Server-side (workload-agnostic)** and **Consumers
   (workload-specific)** sections. §4 lifecycle reframed as the
   server's, not speech-specific. §12 gained a "Forward-pointer:
   post-processing consumer" subsection naming `LlamaServerHost`
   extraction as the trigger.
5. **Phase 5** — Wrote
   [ADR-027](../../docs/decisions/027-llamaserver-namespace-rescope.md).
   Updated memory vault: `memory/decisions/_index.md`,
   `memory/services/platform.md` (refreshed bullet + ADR-027 row),
   `memory/services/desktop.md` (date bump),
   `memory/knowledge/llama-cpp-release-assets.md` (source link).
   `memory/services/tests.md` didn't need changes.
6. **Phase 6** — Marked plan completed; removed from
   `plans/INDEX.md`; full clean rebuild from cold (`obj/` and `bin/`
   wiped); all three test projects green from cold rebuild.

## Decisions Made

- **Flat namespace `Parlotype.Core.LlamaServer`** (not
  `Runtime.LlamaServer` or `Inference.LlamaServer`). llama.cpp is a
  concrete external tool, not an abstract category. Post-processing
  will get its own sibling namespace and *consume* `LlamaServer`,
  same as `Speech` does today.
- **`ILlamaCppServerLifecycle` stays on `LlamaCppSpeechRecognizer`
  for now.** Extraction to a dedicated `LlamaServerHost` is deferred
  to the moment the first post-processor lands — flagged in ADR-027
  under "Open follow-ups (1)".
- **`LlamaCppSpeechRecognizer` stays in `Parlotype.Platform.Speech`.**
  It's a *speech consumer* of `LlamaServer`, not the server itself.
- **`Gemma4ModelInfo` + `SpeechEngine` + `SettingsKeys.SpeechEngine`
  stay in `Speech.*`.** They are speech-specific.
- **Settings keys (`LlamaCppActiveInstall`, `LlamaCppServerFolder`,
  `LlamaCppPort`) are unchanged** — stable user-facing keys.
- **On-disk layout unchanged** — `%LOCALAPPDATA%\parlotype\llama-servers\`
  + `manifest.json` schema preserved.
- **Architecture doc renamed in place rather than split**
  (`llamacpp-integration.md` → `llamacpp-subsystem.md`). Single doc
  with Server-side + Consumers sections; speech-recognizer subsection
  splitting deferred until there are multiple consumers documented.
- **Tests mirror prod layout** — moved `Tests/Speech/LlamaServer/` to
  `Tests/LlamaServer/` for symmetry.
- **One commit for the whole refactor.** The plan said "one commit
  per phase"; in practice all six phases were executed in one
  session without intermediate commits. Bundling is honest (and the
  diff per phase is recoverable from the staged state). Future
  refactors of this size should commit per-phase to honour the plan.

## Facts Learned

- **C# scope rule bites moved namespaces.** A class in
  `Parlotype.Platform.Speech.LlamaServer` had implicit access to
  types in the parent `Parlotype.Platform.Speech` namespace. After
  moving to `Parlotype.Platform.LlamaServer`, those references break
  silently — the compiler error names the missing type but doesn't
  hint at the parent-namespace cause. Fix: add explicit `using
  Parlotype.Platform.Speech;`. Hit twice in this refactor
  (`LlamaServerInstaller` → `StreamingFileDownloader`,
  `LlamaCppSpeechRecognizer` → `LlamaCppServerInfo`).
- **Grep with regex anchors misses fully-qualified references.**
  My initial sweep used `using Parlotype\.Platform\.Speech\.LlamaServer;`
  (with semicolon) — that missed two fully-qualified call sites in
  `LlamaCppSettingsViewModel.cs` (`Platform.Speech.LlamaServer.LlamaServerInstaller.BuildInstallId(...)`).
  Always run a second grep without the terminator. Captured as a
  takeaway in the build error, not new vault knowledge.
- **PowerShell batch text-replace via `[System.IO.File]::WriteAllText`
  with a no-BOM UTF-8 encoding is the right tool for mechanical
  cross-file refactors on Windows.** Avoids `Set-Content` BOM
  quirks; preserves trailing newlines via `Get-Content -Raw`.

## Open Blockers

None. Plan completed cleanly, tests green from cold rebuild.

## Documentation Status

- ADR: **done** — [ADR-027](../../docs/decisions/027-llamaserver-namespace-rescope.md) captures the rescope decision + 3 explicit open follow-ups.
- Vault (services/architecture): **done** —
  `docs/architecture/llamacpp-subsystem.md` (renamed + rescoped),
  `memory/services/platform.md`,
  `memory/services/desktop.md`,
  `memory/decisions/_index.md`,
  `memory/knowledge/llama-cpp-release-assets.md`.
- Knowledge (non-derivable facts): **none required.** ADR-027's
  "Open follow-ups" section captures the durable design notes; no
  new third-party / environment facts surfaced beyond what's
  already in `memory/knowledge/llama-cpp-release-assets.md`.

## Next Action

Two natural next steps depending on appetite:

1. **(Lightweight)** Push the branch and open the PR (or extend the
   existing PR for the original plan). The phase-7 commit (`1094f73`)
   and phase-8 commit (`db107bf`) are on the same branch
   `claude/jovial-wozniak-0a3ee5`. The new commit from today should
   join them. Manual desktop smoke check is still pending — open the
   app and verify Settings → llama.cpp renders unchanged.
2. **(Heavyweight)** Begin the **post-processing** feature plan
   (`Whisper → text → llama.cpp LLM → text injector`). When that work
   starts, the first task is the `LlamaServerHost` extraction
   (ADR-027 Open follow-up #1) so the spawned process can be shared
   between speech and post-processing consumers. That refactor unlocks
   adding the new `Parlotype.Core.Postprocessing` namespace + a
   `LlamaCppPostprocessor` Platform implementation + Settings UI for
   model selection.

Start with #1 unless the post-processing work is being scheduled
immediately.
