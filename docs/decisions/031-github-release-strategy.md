---
status: accepted
amended-by: 049
date: 2026-05-24
---

> **Amended by [ADR-049](049-drop-whisper-cuda-runtime.md) (2026-07-31).** Point 2 below
> (the Full/Lite matrix) no longer applies: with the CUDA runtime gone there is nothing to
> split on, so each tag publishes a single `Parlotype-<version>-win-x64.zip` from one build
> leg. Everything else — the tag trigger, self-contained-no-trim packaging, the test gate,
> and the two-stage job layout — is unchanged.

# 031. GitHub Release Strategy

## Context

Parlotype had no way to produce downloadable binaries for end users. The only CI
workflow was `benchmark.yml` (PR-gated regression checks). End users do not have the
.NET 10 runtime installed, so any distributed build must be **self-contained**.

Two facts about the codebase shape the packaging:

- The app is **Windows-only** today — audio capture goes through NAudio/WASAPI. macOS
  and Linux are planned but not yet implemented. The only meaningful release RID is
  `win-x64`.
- `Parlotype.Platform.csproj` ships two GPU runtimes: `Whisper.net.Runtime.Cuda`
  (~350 MB, NVIDIA-only, included when `EnableCuda=true`, the default) and
  `Whisper.net.Runtime.Vulkan` (~30 MB, always included). The `EnableCuda` MSBuild flag
  already exists (ADR-012, ADR-022) precisely so a CUDA-free build can be produced.

The CUDA runtime adds meaningful weight to the artifact. Shipping a single build forces
either every user to download CUDA libraries they may never use, or NVIDIA users to lose
the CUDA path. Neither is acceptable as the only option. (The `EnableCuda` flag is named
for a ~350 MB *NuGet package*; the marginal cost in the *published self-contained output*
is ~150 MB — see the Decision section.)

Gemma 4 GGUF weights and the `llama-server` binary are downloaded on demand at runtime
(ADR-026, ADR-029), so they are never bundled into a release artifact.

## Decision

Releases are produced by a new `.github/workflows/release.yml`, triggered by pushing a
`v*` git tag. Each release publishes **two** self-contained `win-x64` zips:

1. **Trigger** — push of a tag matching `v*`. The version is derived from the tag
   (`v1.2.3` → `1.2.3`) and passed to the build via `-p:Version=`. Tags containing a
   hyphen (e.g. `v1.2.3-beta`) mark the GitHub Release as a pre-release. Pushes to
   `master` do **not** publish — they remain CI-only.
2. **Full vs Lite artifacts** — a build matrix produces:
   - **Full** (`EnableCuda=true`): CUDA + Vulkan, for NVIDIA users.
   - **Lite** (`EnableCuda=false`): Vulkan-only, works on all GPUs and CPU.
   The same `EnableCuda` value gates that variant's test step, so the matrix legs are
   fully independent. (Measured self-contained `win-x64` output: Lite ~720 MB, Full
   ~870 MB unzipped. The CUDA package adds the ~150 MB `ggml-cuda-whisper.dll`; it relies
   on the user's installed CUDA toolkit for `cudart`/`cublas`, so it does not bundle them.)
3. **Self-contained folder, zipped** — `dotnet publish -r win-x64 --self-contained true`,
   then `Compress-Archive`. **No single-file** and **no trimming**: Avalonia and
   CommunityToolkit.Mvvm are reflection-heavy, and the GPU runtimes ship many native DLLs
   that single-file/trim handles poorly. A plain published folder is the robust choice.
4. **Test gate** — each matrix leg runs `dotnet test` before publishing, so a tag cut from
   a broken commit fails the release rather than shipping it.
5. **Two-stage jobs** — the matrix build runs on `windows-latest`; a separate lightweight
   `release` job on `ubuntu-latest` downloads both artifacts and publishes the GitHub
   Release once via `softprops/action-gh-release`, avoiding a race where each matrix leg
   tries to create the same release.

## Consequences

- **Easier:** Cutting a release is a single `git tag vX.Y.Z && git push --tags`. Users get
  a download matched to their hardware without the extra CUDA weight on non-NVIDIA machines.
- **Easier:** The pre-existing `EnableCuda` flag does all the heavy lifting for the
  Full/Lite split — no new build infrastructure or conditional packaging logic.
- **Harder:** Two matrix legs each restore + test + publish, roughly doubling release CI
  time and minutes versus a single build. Acceptable for an infrequent, tag-triggered job.
- **Harder:** `win-x64` only. macOS/Linux releases will need new RIDs (and a non-NAudio
  capture path) when those platforms land — a future ADR.
- **Note:** Artifacts are unsigned, so Windows SmartScreen will warn on first run. Code
  signing is deferred; it can be added to the publish step later without changing this
  strategy.
- **Note:** No in-app auto-update — users re-download from the Releases page. An installer
  + auto-update path (e.g. Velopack) was considered and deferred to keep the first
  iteration dependency-free.
