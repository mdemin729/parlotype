---
title: Managed llama.cpp server installer
status: planned
created: 2026-05-17
started:
completed:
---

# Managed llama.cpp Server Installer

## Problem

Parlotype currently requires users to **manually download `llama-server.exe`**, unpack it somewhere on disk, and point Settings → llama.cpp at that folder via `LlamaCppServerFolder`. The sidecar lifecycle in [LlamaCppSpeechRecognizer.cs:223](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs) reads exactly one folder and spawns `llama-server.exe` from it.

This is friction-heavy and error-prone: users must know which build variant to pick (Vulkan vs CUDA 12.4 vs CUDA 13.1 vs CPU), find the correct release on GitHub, extract the right archive into the right place, and remember to fetch the CUDA runtime DLLs as a separate companion zip.

## Goal

Let users **browse, install, and manage multiple llama.cpp server builds** from inside the Settings UI, while keeping the existing "point at a folder" path as an explicitly **manual** mode rendered with a distinct visual treatment.

User-facing requirements:

1. After installing Parlotype, no llama server is on disk — that's expected.
2. The llama.cpp settings page lists available builds from GitHub (Vulkan, CUDA 12.4, CUDA 13.1, CPU, …) filtered to the current OS/arch.
3. User can install one or more builds side-by-side; install is one click; cancellable.
4. User can check for updates against the latest GitHub release.
5. User can also point at a manually-installed folder (existing behaviour) — rendered differently (badge: *"Manual — not managed by Parlotype"*).
6. User can switch which install is active. The sidecar must restart cleanly when switched.

## Scope decisions

- **Phase-1 OS:** Windows only. macOS/Linux tar.gz extraction deferred to a follow-up plan (the parser and most of the architecture are cross-platform; only the extractor + UI filter need extending).
- **Checksums:** verify SHA256 when the GitHub Releases API exposes `digest`; warn-and-continue when absent.
- **Manual mode kept:** no migration; existing users' `LlamaCppServerFolder` continues to work and is shown alongside managed installs with a distinct visual treatment.

## Out of scope

- macOS / Linux extraction.
- Background auto-update or auto-install.
- Authenticated GitHub API (no PAT input).
- Pinning to a specific build via config file (UI-only selection for now).
- Verifying ggml-* DLL ABI compatibility with a given Gemma model — left to the runtime sidecar to fail fast.

## Phased workplan

Each phase = one reviewable commit. Detail in [implementation-plan.md](implementation-plan.md).

- [ ] Phase 1 — Core contracts (enums, records, interfaces in `Parlotype.Core/Speech/LlamaServer/`)
- [ ] Phase 2 — Manifest + registry (`JsonLlamaServerRegistry` in Platform + tests)
- [ ] Phase 3 — GitHub catalog (`GitHubLlamaServerCatalog` + `LlamaServerAssetParser` + ETag cache + tests)
- [ ] Phase 4 — Installer (`LlamaServerInstaller` with staging-dir + cudart companion + SHA256 + tests)
- [ ] Phase 5 — Recognizer wiring (`LlamaCppActiveInstall` setting + path resolution + `ILlamaCppServerLifecycle`)
- [ ] Phase 6 — DI + dialog generalization (register services + extract reusable download modal)
- [ ] Phase 7 — Settings UI rework (sections: Installed, Manual, Available, Update banner)
- [ ] Phase 8 — ADR-026 + architecture doc update + memory vault

## Verification

End-to-end manual flow on Windows after each UI-touching phase:

1. `dotnet build Parlotype.slnx` clean, `dotnet test` green.
2. Delete `%LOCALAPPDATA%\parlotype\llama-servers\` + `LlamaCppActiveInstall` to simulate fresh user.
3. Settings → llama.cpp → **Available** lists current GitHub releases filtered to Windows x64 (Vulkan, CUDA 12.4, CUDA 13.1, CPU).
4. Install Vulkan build. Verify atomic-rename (no partial folder on mid-download cancel); folder appears at `%LOCALAPPDATA%\parlotype\llama-servers\b{N}-win-vulkan-x64\` with `llama-server.exe`.
5. Install CUDA 12.4 build. Verify cudart DLLs are present in the same folder.
6. Set active = Vulkan install. Dictate — sidecar spawns from that folder, `/health` ready, transcription works.
7. Switch active = CUDA 12.4. Vulkan sidecar is killed and CUDA sidecar starts.
8. Add a manual install via folder picker. Renders with **"Manual — not managed by Parlotype"** badge and distinct background.
9. Switch active = Manual. Sidecar respawns from that folder.
10. Uninstall an active managed install. Sidecar stops, folder removed, active reverts.
11. "Check for updates" — newer `b{N}` in catalog vs active install's build → banner appears.
12. Pull network mid-download. Cancel works, staging dir cleaned, no partial install in `manifest.json`.

Plus headless UI tests in `Parlotype.Desktop.Tests` covering listing, install command invocation, set-active flow, manual badge visibility (using `MockLlamaServerCatalog`, `MockLlamaServerInstaller`, `MockLlamaServerRegistry`).
