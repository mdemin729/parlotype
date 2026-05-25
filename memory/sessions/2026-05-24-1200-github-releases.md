---
title: "Session: 2026-05-24 — GitHub Releases"
type: session
status: active
tags: [ci, release, cuda, packaging]
created: 2026-05-24
summary: Added tag-triggered GitHub Actions release workflow producing Full (CUDA+Vulkan) and Lite (Vulkan-only) self-contained win-x64 zips
---

# Session: 2026-05-24 — GitHub Releases

## Active Focus
- `.github/workflows/release.yml` (new) — release automation
- `docs/decisions/031-github-release-strategy.md` (new ADR)
- `README.md` — new "Download / Releases" section
- `memory/decisions/_index.md`, `memory/knowledge/` — vault updates

## Decisions Made
- Releases trigger on `v*` git tags; version derived from tag (`v1.2.3` → `1.2.3` via
  `-p:Version=`); hyphenated tags (`v1.2.3-beta`) → pre-release. Pushes to `master` stay CI-only.
- Ship **two** self-contained `win-x64` zips per release via build matrix:
  **Full** (`EnableCuda=true`, CUDA+Vulkan) and **Lite** (`EnableCuda=false`, Vulkan-only).
- No single-file, no trimming (Avalonia/MVVM reflection + native GPU DLLs). Plain published
  folder, zipped with `Compress-Archive`.
- Two-stage jobs: `windows-latest` matrix build (restore → test gate → publish → zip → upload
  artifact) + `ubuntu-latest` release job aggregating both zips into one
  `softprops/action-gh-release@v2` call (avoids matrix race on release creation).
- Code signing and in-app auto-update (Velopack) explicitly **deferred** — documented in ADR-031.

## Facts Learned
- Self-contained `win-x64` publish is large: **Lite ~720 MB, Full ~870 MB unzipped**.
- `Whisper.net.Runtime.Cuda` adds only `ggml-cuda-whisper.dll` (~150 MB) to the published
  output and does **not** bundle `cudart`/`cublas` — the Full build still requires the user's
  installed CUDA toolkit. Captured in `memory/knowledge/whisper-cuda-runtime-packaging.md`.
- `-p:EnableCuda=false` at publish correctly propagates from the Desktop publish through the
  `Parlotype.Platform` project reference (global MSBuild property) — Lite output verified to
  contain zero CUDA DLLs and the Vulkan natives.

## Open Blockers
- None. Workflow is untested on real CI — first real validation is cutting a `v*` tag (or a
  throwaway `v0.0.0-rc1` pre-release tag) and inspecting the produced artifacts/Release.

## Documentation Status
- ADR: done — `docs/decisions/031-github-release-strategy.md`
- Vault (services/architecture): done — `memory/decisions/_index.md` row added
- Knowledge (non-derivable facts): done — `memory/knowledge/whisper-cuda-runtime-packaging.md` + index row

## Next Action
Push the branch / open a PR (commit `a010743` on `claude/interesting-elbakyan-d83519`, not yet
pushed). After merge to `master`, validate by pushing a throwaway tag `v0.0.0-rc1`, confirm both
zips are produced and the Release is marked pre-release, then delete the tag/Release. Optional
follow-ups: code signing, auto-update/installer (Velopack), macOS/Linux RIDs once those platforms land.
