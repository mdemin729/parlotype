# Implementation Plan — Rescope `LlamaServer` Out of `Speech.*`

Sibling overview: [task.md](task.md).

---

## Affected surface

Mechanical refactor across **~28 files in 5 projects**. No `.csproj`
changes, no DI rewiring, no API-shape changes.

| Where | Files | Action |
|---|---|---|
| `src/Parlotype.Core/Speech/LlamaServer/` | 13 | Move to `src/Parlotype.Core/LlamaServer/`; namespace `Parlotype.Core.Speech.LlamaServer` → `Parlotype.Core.LlamaServer` |
| `src/Parlotype.Platform/Speech/LlamaServer/` | 5 | Move to `src/Parlotype.Platform/LlamaServer/`; namespace `Parlotype.Platform.Speech.LlamaServer` → `Parlotype.Platform.LlamaServer` |
| `src/Parlotype.Platform/Speech/LlamaCppServerInfo.cs` | 1 | Move to `src/Parlotype.Platform/LlamaServer/LlamaCppServerInfo.cs` (server concern, not speech); namespace `Parlotype.Platform.Speech` → `Parlotype.Platform.LlamaServer` |
| `src/Parlotype.Tests/Speech/LlamaServer/` | 6 | Move to `src/Parlotype.Tests/LlamaServer/`; namespace `Parlotype.Tests.Speech.LlamaServer` → `Parlotype.Tests.LlamaServer` |
| `src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs` | 1 | **Stays put.** Update `using` to new namespaces |
| `src/Parlotype.Platform/PlatformServiceExtensions.cs` | 1 | Update two `using` lines |
| `src/Parlotype.Desktop/**/*.cs` | 5 | Update `using` lines only (Services, ViewModels/Settings, App.axaml.cs) |
| `src/Parlotype.Desktop.Tests/**/*.cs` | 3 | Update `using` lines only |

---

## Phases

### Phase 1 — Move Core namespace

1. `git mv src/Parlotype.Core/Speech/LlamaServer src/Parlotype.Core/LlamaServer` (13 files).
2. Swap namespace in each moved file:
   `namespace Parlotype.Core.Speech.LlamaServer;` →
   `namespace Parlotype.Core.LlamaServer;`
3. Update consumers across the solution:
   `using Parlotype.Core.Speech.LlamaServer;` →
   `using Parlotype.Core.LlamaServer;` (~22 sites).
4. `dotnet build Parlotype.slnx -p:EnableCuda=false` clean.
5. Run all three test projects; 429 passing.

**Critical files:** all 13 in
[src/Parlotype.Core/Speech/LlamaServer/](../../src/Parlotype.Core/Speech/LlamaServer/)
plus every `using` site (Grep `Parlotype\.Core\.Speech\.LlamaServer` finds
them all).

### Phase 2 — Move Platform namespace + relocate `LlamaCppServerInfo`

1. `git mv src/Parlotype.Platform/Speech/LlamaServer src/Parlotype.Platform/LlamaServer` (5 files).
2. `git mv src/Parlotype.Platform/Speech/LlamaCppServerInfo.cs src/Parlotype.Platform/LlamaServer/LlamaCppServerInfo.cs`.
3. Swap namespaces:
   - Moved-folder files: `Parlotype.Platform.Speech.LlamaServer` → `Parlotype.Platform.LlamaServer`.
   - `LlamaCppServerInfo.cs`: `Parlotype.Platform.Speech` → `Parlotype.Platform.LlamaServer`.
4. Update consumers:
   - [LlamaCppSpeechRecognizer.cs](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs)
     — add `using Parlotype.Platform.LlamaServer;` for `LlamaCppServerInfo`
     (and re-import anything from the moved folder).
   - [LlamaCppSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/LlamaCppSettingsViewModel.cs)
     — same. (Yes, the VM importing `LlamaCppServerInfo` directly is the
     leakage noted in `docs/architecture/llamacpp-integration.md` §11; the
     rename makes it slightly more visible but does **not** fix it.
     `ILlamaCppServerProbe` promotion to Core is a separate ADR.)
   - [PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs)
     — update the two `using` lines.
   - Tests under `Parlotype.Tests/Speech/LlamaServer/` (still in the old
     location at this phase) — update their `using` lines.
5. Build + tests clean.

**Critical files:**
[src/Parlotype.Platform/Speech/LlamaServer/](../../src/Parlotype.Platform/Speech/LlamaServer/),
[src/Parlotype.Platform/Speech/LlamaCppServerInfo.cs](../../src/Parlotype.Platform/Speech/LlamaCppServerInfo.cs),
[src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs),
[src/Parlotype.Platform/PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs).

### Phase 3 — Move test folder

1. `git mv src/Parlotype.Tests/Speech/LlamaServer src/Parlotype.Tests/LlamaServer` (6 files).
2. Swap namespace `Parlotype.Tests.Speech.LlamaServer` →
   `Parlotype.Tests.LlamaServer` in each moved file.
3. No other `using` updates needed (no test references another by
   fully-qualified name).
4. Build + tests clean.

**Critical files:** the six files under
[src/Parlotype.Tests/Speech/LlamaServer/](../../src/Parlotype.Tests/Speech/LlamaServer/).

### Phase 4 — Rescope architecture doc

1. `git mv docs/architecture/llamacpp-integration.md docs/architecture/llamacpp-subsystem.md`.
2. Rewrite the intro:
   - Old scope: *"the runtime integration between Parlotype and the
     `llama-server.exe` sidecar used to run Gemma 4 as an alternative
     speech recognition engine"*.
   - New scope: *"the managed `llama-server` subsystem and its consumers.
     Today there is one consumer (`LlamaCppSpeechRecognizer` for Gemma 4
     transcription); a future post-processing consumer will share the
     same installed binary, settings, and lifecycle."*
3. Section restructure:
   - §1 *Component Overview*: split into **Server-side** (catalog,
     registry, installer, lifecycle, settings) and **Consumers** (today:
     speech only).
   - §4 *llama-server Lifecycle*: clarify that the state machine
     describes the **server** lifecycle, not a speech-specific one.
   - §12 *Server Installation Lifecycle*: append a one-paragraph
     forward-pointer noting that the installed binary will be consumed
     by post-processing in a future ADR.
4. Update every cross-link in the repo. Grep for `llamacpp-integration.md`
   and rewrite each hit to `llamacpp-subsystem.md`:
   - [docs/decisions/025-gemma4-llamacpp-desktop.md](../../docs/decisions/025-gemma4-llamacpp-desktop.md)
   - [docs/decisions/026-managed-llama-server-install.md](../../docs/decisions/026-managed-llama-server-install.md)
   - [memory/services/platform.md](../../memory/services/platform.md)
   - [memory/knowledge/llama-cpp-release-assets.md](../../memory/knowledge/llama-cpp-release-assets.md)
   - Anywhere else the grep finds.

**Critical files:** the renamed doc + every cross-link site.

### Phase 5 — ADR-027 + memory-vault refresh

1. Create `docs/decisions/027-llamaserver-namespace-rescope.md` from
   `docs/decisions/_template.md`:
   - **Status:** Accepted; **Date:** 2026-05-18.
   - **Context:** ADR-025/-026 placed the server-lifecycle types under
     `Parlotype.*.Speech.LlamaServer` when the only consumer was the
     speech recognizer. Planned post-processing work will add a second
     consumer to the same server.
   - **Decision:** Move `Parlotype.*.Speech.LlamaServer.*` →
     `Parlotype.*.LlamaServer.*`. Relocate `LlamaCppServerInfo` to
     `Parlotype.Platform.LlamaServer`. Mirror in tests. No API-shape, no
     schema, no settings changes.
   - **Consequences:** Honest namespace; post-processing consumer slots
     in cleanly. **Temporary asymmetry:**
     `ILlamaCppServerLifecycle` is still implemented by the speech
     recognizer; promoting that to a dedicated `LlamaServerHost` is a
     follow-up (called out in this ADR's *Open follow-ups* section) that
     becomes mandatory when the first post-processor lands so two
     consumers can share the spawned process.
   - **Alternatives Considered:** `Parlotype.Core.Runtime.LlamaServer`
     (premature umbrella), `Parlotype.Core.Inference.LlamaServer`
     (imposes a workload category on lifecycle code).
   - **Related:** ADR-025, ADR-026.
2. [memory/decisions/_index.md](../../memory/decisions/_index.md) — add
   ADR-027 row.
3. [memory/services/platform.md](../../memory/services/platform.md) —
   replace `Parlotype.Platform.Speech.LlamaServer` and
   `Parlotype.Platform.Speech.LlamaCppServerInfo` with the new paths.
4. [memory/services/desktop.md](../../memory/services/desktop.md) —
   refresh paragraphs that name the Core namespace.
5. [memory/services/tests.md](../../memory/services/tests.md) — refresh
   if it references the llama-server tests by path.
6. [memory/knowledge/llama-cpp-release-assets.md](../../memory/knowledge/llama-cpp-release-assets.md)
   — update the link
   `[LlamaServerAssetParser.cs](../../src/Parlotype.Platform/Speech/LlamaServer/LlamaServerAssetParser.cs)`
   to the new path.

**Critical files (new):** `docs/decisions/027-llamaserver-namespace-rescope.md`.

**Critical files (modified):**
[memory/decisions/_index.md](../../memory/decisions/_index.md),
[memory/services/platform.md](../../memory/services/platform.md),
[memory/services/desktop.md](../../memory/services/desktop.md),
[memory/knowledge/llama-cpp-release-assets.md](../../memory/knowledge/llama-cpp-release-assets.md).

### Phase 6 — Plan housekeeping + final verification

1. [task.md](task.md): `status: completed`, set `completed: 2026-05-18`,
   tick all phase checkboxes.
2. [plans/INDEX.md](../INDEX.md): remove the row for this plan (per
   `plans/WORKFLOW.md` — completed plans are removed from INDEX; git
   history preserves them).
3. Full clean: delete `obj/` and `bin/` across the solution; rebuild.
4. `dotnet build Parlotype.slnx -p:EnableCuda=false` — zero warnings.
5. Run all three test projects — **429 passing** (no test count change).
6. `dotnet run --project src/Parlotype.Desktop` — verify Settings →
   llama.cpp renders the Active server / Installed / Manual / Available
   sections exactly as before.

---

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| `using` site missed during rename → compile error | Compiler catches it. Build after every phase. |
| Test name collision (two `LlamaServerInstaller` symbols if rename half-done) | Phases are atomic — namespace + folder move + consumer updates land in one commit per phase. No interleaved state. |
| Doc cross-links go stale | Grep the repo for `llamacpp-integration.md` and update each hit in the same commit as the rename. |
| Hidden internal-visibility cracks | `Parlotype.Platform.csproj` already lists `Parlotype.Tests` and `Parlotype.Desktop.Tests` as `InternalsVisibleTo`. Internal members keep working post-rename. |
| Memory-vault references rot | Phase 5 explicitly enumerates the files that reference the old namespace. |
| ADR-025 link to `llamacpp-integration.md` | Update the link mechanically in Phase 4 alongside the rename — ADR-025's text body is otherwise immutable. |

---

## Definition of Done checklist

1. ☐ `dotnet build Parlotype.slnx -p:EnableCuda=false` clean after every phase.
2. ☐ `dotnet test` stays at **429 passing** after every phase (zero new failures, zero skipped).
3. ☐ ADR-027 written (Phase 5).
4. ☐ Memory vault updated: `memory/services/platform.md`,
   `memory/services/desktop.md`, `memory/decisions/_index.md`,
   `memory/knowledge/llama-cpp-release-assets.md` (Phase 5).
5. ☐ Desktop smoke check confirms the Settings → llama.cpp page renders
   unchanged (Phase 6).
6. ☐ `git log --follow` works across the file moves (history preserved).
