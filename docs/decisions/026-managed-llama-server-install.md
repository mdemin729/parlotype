---
status: accepted
date: 2026-05-17
---

# 026. Managed llama.cpp Server Installation

## Context

ADR-025 introduced `llama-server.exe` as a sidecar process powering Gemma 4
transcription, but required the user to hand-download the binary, unpack the
correct release archive, and point `LlamaCppServerFolder` at the resulting
folder. That worked for the initial integration but is friction-heavy:

- llama.cpp publishes a different archive per backend × OS × architecture
  (Vulkan, CUDA 12.4, CUDA 13.1, CPU x64/arm64, HIP, SYCL, …). The right
  variant depends on the user's GPU and driver, which non-technical users
  can't necessarily identify.
- CUDA-on-Windows builds need a *second* archive (`cudart-llama-bin-*.zip`)
  to provide the NVIDIA runtime DLLs when the CUDA Toolkit isn't installed.
- New llama.cpp releases (typically several per week) ship as
  `b{number}` tags with no "latest" alias; users have to manually re-check
  GitHub and re-download to upgrade.
- A single configured folder pre-empted side-by-side installs — e.g. for
  comparing Vulkan against CUDA performance, or keeping a known-good build
  while testing a newer one.

Goal: let Parlotype browse, install, and manage multiple llama.cpp server
builds directly from the Settings UI, while preserving the manual
folder-picker path for power users who want to keep their own copy.

## Decision

Add a first-class **managed-install** subsystem in `Parlotype.Core/Speech/
LlamaServer/` (contracts) and `Parlotype.Platform/Speech/LlamaServer/`
(implementations), surface it via a new Settings UI, and keep
`LlamaCppServerFolder` working as an explicitly-labelled "Manual" mode.

### Storage layout

```
%LOCALAPPDATA%\parlotype\llama-servers\
   .staging\{guid}\               # in-progress install (atomic-rename pattern)
      payload\                    # extracted archive contents (renamed into place on success)
      main.zip / companion.zip    # downloaded archives
   .cache\releases.json           # GitHub catalog cache + ETag + parsed snapshot
   manifest.json                  # source of truth: managed install entries
   b9198-win-cuda-12.4-x64\
      llama-server.exe
      ggml-cuda.dll
      cudart64_12.dll             # cudart companion zip merged in for CUDA Windows variants
   b9198-win-vulkan-x64\
      llama-server.exe
      ggml-vulkan.dll
```

`manifest.json` is the source of truth (folder names are convenient but not
load-bearing). On corrupt-read, the manifest is quarantined to
`manifest.json.bak.{timestamp}` and the registry starts fresh.

### Core contracts (`Parlotype.Core/Speech/LlamaServer/`)

- **Enums** `LlamaServerBackend` / `LlamaServerSource` / `LlamaServerOs` /
  `LlamaServerArch`, each with an `Unknown` member so the parser stays
  tolerant of future variants.
- **Records** `LlamaServerVariant` (catalog entry), `LlamaServerInstall`
  (read model), `LlamaServerManagedInstallRecord` (write model with audit
  fields), `LlamaServerReleaseGroup`, `LlamaServerInstallProgress`.
- **Interfaces** `ILlamaServerCatalog`, `ILlamaServerInstaller`,
  `ILlamaServerRegistry`, `ILlamaCppServerLifecycle`.
- New setting: `SettingsKeys.LlamaCppActiveInstall` = `managed:{id}` |
  `manual` | (empty).

### Platform implementations

- `JsonLlamaServerRegistry` — read/write `manifest.json`; active selector
  lives in `ISettingsService` (not the manifest), and manual mode resolves
  through the existing `LlamaCppServerFolder` setting.
- `LlamaServerAssetParser` + `GitHubLlamaServerCatalog` — pure string-split
  parser; HTTP client with `User-Agent: parlotype/{version}` and
  `If-None-Match` ETag; 1 h on-disk cache; OS/arch filter applied at read
  time so a single cache works across machines.
- `LlamaServerInstaller` — disk-space precheck (`bytes * 3` headroom),
  download main asset, optional CUDA companion, SHA256 verify when present
  (warn-and-continue when absent), `ZipFile.ExtractToDirectory` to a
  staging payload dir, atomic `Directory.Move` into `{id}/`, manifest
  update. Failure cleans up the staging dir; cancellation throws and
  leaves no on-disk state.
- `StreamingFileDownloader` — small shared helper extracted from the
  existing Whisper download loop; both Whisper and llama-server installs
  use it. `HttpModelDownloadService` delegates its download chunk-loop
  here (no behaviour change for Whisper).
- `LlamaCppSpeechRecognizer` additionally implements
  `ILlamaCppServerLifecycle`; `GetServerPathAsync` consults the active
  managed install first, then falls through to `LlamaCppServerFolder`,
  then to the legacy default folder. The installer calls
  `StopForReplacementAsync` before deleting a folder that's currently
  active (Windows file-lock release).

### Desktop UI

`LlamaCppSettingsViewModel` gains `Installed` and `Available` observable
collections plus commands `InstallVariant` / `UninstallInstall` /
`SetActiveManaged` / `SetActiveManual` / `CheckForUpdates`. The view
[`LlamaCppSettingsView.axaml`](../../src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml)
is reorganized into sections: Active server (status + Managed/Manual
badge), Update banner, Installed (RadioButton per row), Manual install
(distinct background + "Not managed by Parlotype" badge), Available
builds, port + Save/Reset. The generalized `ModelDownloadDialog` is
reused for install progress via a Desktop wrapper
`LlamaServerInstallDialogService` (implements `ILlamaServerInstaller`,
overrides the Platform default via `AddSingleton<ILlamaServerInstaller,
LlamaServerInstallDialogService>()` in `App.axaml.cs` — last-wins).

### Key design choices

1. **Manifest as the source of truth** (not folder scanning). Folder names
   match install ids by convention but the manifest carries provenance —
   asset name, companion asset name, SHA256, install timestamp.
2. **Active selector in `ISettingsService`**, not in the manifest. The
   manifest stays a pure list of managed installs; the
   `LlamaCppActiveInstall` setting holds `managed:{id}` or `manual`.
3. **Staging dir + atomic `Directory.Move`** — every install assembles
   under `.staging/{guid}/payload/` and is committed by a single rename.
   Crash mid-install leaves no visible state; the staging dir is removed
   in a `finally` block.
4. **SHA256 verify when GitHub provides `digest`**, warn-and-continue
   when absent. GitHub Releases assets gained `digest` (SHA256) in 2024,
   but older releases lack it; refusing to install old assets would be
   surprising.
5. **Tolerant asset parser.** Unknown backend strings become
   `LlamaServerBackend.Unknown` and the catalog filters them out, but the
   parser doesn't throw on a new release format — only structurally
   invalid names like `source.zip` are rejected.
6. **CUDA companion handled per-variant**, not as a global rule —
   `LlamaServerVariant.CompanionAssetName/DownloadUrl/Bytes/Sha256` carry
   the cudart pairing so non-CUDA variants are unaffected.
7. **Manual mode preserved with distinct treatment.** No migration:
   existing users with a populated `LlamaCppServerFolder` keep working.
   The "Manual" entry renders in a separate panel with a grey badge and
   "Not managed by Parlotype" label so the source is unambiguous.
8. **Phase-1 scope: Windows only.** macOS/Linux `tar.gz` extraction
   throws `NotSupportedException` and is gated by `LlamaServerOs`. The
   parser handles all platforms (the breadth-of-coverage tests assert
   this), the catalog filter and installer extractor are the only
   Windows-specific bits.
9. **DI split for Desktop wrapping.** Platform registers the installer
   as both `LlamaServerInstaller` (concrete) and `ILlamaServerInstaller`
   (interface, mapped to the concrete). Desktop overrides the interface
   with `LlamaServerInstallDialogService`, which depends on the concrete
   instance. Benchmark / headless callers keep getting the bare installer.

### Architecture

```
Settings → llama.cpp → "Install" on a catalog row
        ↓
LlamaServerInstallDialogService (Desktop)
   shows ModelDownloadDialog (generalized)
        ↓
LlamaServerInstaller (Platform)
   staging dir → StreamingFileDownloader → SHA256 verify
   → ZipFile.ExtractToDirectory → atomic Directory.Move
   → ILlamaServerRegistry.AddOrUpdateAsync
        ↓
manifest.json updated; UI refreshes Installed list
```

## Consequences

### Positive

- Users install a llama-server build in one click without leaving the app.
- Side-by-side installs (Vulkan + CUDA) supported; switch active via
  RadioButton in the Installed list.
- "Check for updates" surfaces a banner when the latest catalog build
  exceeds the active install's build — no background polling.
- Crash-safe: staging dir + atomic rename means a half-finished install
  never appears in the Installed list.
- Manual mode keeps working unchanged; no migration step.
- `StreamingFileDownloader` is now shared, reducing duplication between
  Whisper model downloads and llama-server installs.

### Negative

- Adds ~1 800 LOC across Core/Platform/Desktop + tests for what was
  previously a folder-picker.
- GitHub unauthenticated rate limit (60 req/h) still applies — mitigated
  by 1 h cache + ETag, but a user clicking "Check for updates" frequently
  could trip it.
- macOS/Linux extraction not implemented yet; non-Windows users still
  need the manual folder mode for phase 1.

### Risks

- **Asset naming drift upstream.** llama.cpp has historically renamed
  archives (`win-noavx`, `kompute`, etc. came and went). Mitigated by
  the tolerant parser: unknown variants become `Unknown` and are
  skipped, not fatal.
- **Active sidecar file-lock on Windows.** Uninstalling or replacing the
  active install while the sidecar is running would fail. Mitigated by
  `ILlamaCppServerLifecycle.StopForReplacementAsync`, which the
  installer calls before any destructive file op.
- **ZIP-slip** in extraction. Modern .NET's `ZipFile.ExtractToDirectory`
  already validates entry paths against the destination, but a defensive
  audit is worth doing if Apple/Linux tar.gz support lands later
  (`System.Formats.Tar` validation semantics differ).

## Alternatives Considered

1. **Bundle a fixed llama-server build with Parlotype.** Bloats installer
   by ~30–400 MB depending on backend; freezes users at one llama.cpp
   version; multiplies installer size by chosen backends.
2. **Background auto-update.** Considered; deferred. Background HTTP +
   silent process restarts has surprise-failure modes; the explicit
   "Check for updates" button keeps the user in the loop.
3. **Folder-scan manifest derivation** (don't store `manifest.json`,
   derive from folder names + a single `metadata.json` per folder). More
   moving parts on read, less robust to user-edited folders; rejected.
4. **`System.Formats.Tar` for `.tar.gz` now.** The parser and most of
   the architecture are cross-platform; deferred to keep Phase 1 small
   and Windows-only.
5. **Authenticated GitHub API (PAT input).** Would lift the rate limit
   from 60 to 5 000 req/h, but adds a credentials-input surface we'd
   rather avoid; ETag + 1 h cache is enough for the realistic browsing
   pattern.

## Related

- ADR-025: Gemma 4 via llama.cpp Sidecar in Desktop (this ADR augments,
  not supersedes — manual folder mode still works)
- ADR-007: Whisper Model Selection & Download (pattern for
  Platform-impl + Desktop-dialog-wrapper that this ADR reuses)
- `memory/knowledge/llama-cpp-release-assets.md` — non-derivable asset
  naming + cudart pairing facts captured during this work
- `docs/architecture/llamacpp-subsystem.md` (renamed in ADR-027) §8 (Configuration Surface)
  + new §12 (Server Installation Lifecycle)
- `plans/2026-05-17-llama-server-installer/` — 8-phase implementation
  plan and the per-phase task breakdown
