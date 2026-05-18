# Implementation Plan — Managed llama.cpp Server Installer

Sibling overview: [task.md](task.md).

---

## Architecture overview

### Storage layout (new)

```
%LOCALAPPDATA%\parlotype\llama-servers\
   .staging\{guid}\               # in-progress install (atomic-rename pattern)
   .cache\releases.json           # GitHub catalog cache + ETag
   manifest.json                  # registry of managed installs
   b9198-win-cuda-12.4-x64\
      llama-server.exe
      ggml-cuda.dll
      cudart64_12.dll             # cudart companion zip merged in
      ...
   b9198-win-vulkan-x64\
      llama-server.exe
      ggml-vulkan.dll
      ...
```

`manifest.json` is the **source of truth** — folder names are convenient but not load-bearing. Schema:

```jsonc
{
  "version": 1,
  "installs": [
    {
      "id": "b9198-win-cuda-12.4-x64",
      "build": "b9198",
      "backend": "Cuda12",
      "os": "Windows",
      "arch": "X64",
      "path": "b9198-win-cuda-12.4-x64",        // relative to llama-servers/
      "assetName": "llama-b9198-bin-win-cuda-12.4-x64.zip",
      "companionAssetName": "cudart-llama-bin-win-cuda-12.4-x64.zip",
      "sha256": "8c79a9b226de4b3ca...",        // null if asset had no digest
      "installedAt": "2026-05-17T10:23:00Z"
    }
  ]
}
```

### Settings key changes

- **Keep** `LlamaCppServerFolder` (manual mode path; preserves existing user setup).
- **Add** `LlamaCppActiveInstall` — value is `managed:{id}` (e.g. `managed:b9198-win-cuda-12.4-x64`) or `manual` or empty.

`LlamaCppSpeechRecognizer.GetServerPathAsync` ([LlamaCppSpeechRecognizer.cs:223](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs)) consults `LlamaCppActiveInstall` first:
- `managed:{id}` → resolve via `ILlamaServerRegistry`
- `manual` or empty → fall back to today's `LlamaCppServerFolder` logic (unchanged behaviour for existing users)

### Core ↔ Platform boundary

All HttpClient, JSON parsing, regex, OS detection, and file IO stay in `Parlotype.Platform`. Core only gets pure DTOs and interfaces. (Phase 5 also fixes the existing leak where `LlamaCppSettingsViewModel` imports `LlamaCppServerInfo` directly from Platform — promoted behind a new `ILlamaCppServerProbe` Core interface.)

---

## New code surface

### `src/Parlotype.Core/Speech/LlamaServer/`

- `LlamaServerBackend.cs` — enum `{ Cpu, Cuda12, Cuda13, Vulkan, Hip, Sycl, Metal, KleidiAi, Unknown }`
- `LlamaServerSource.cs` — enum `{ Managed, Manual }`
- `LlamaServerOs.cs` — enum `{ Windows, MacOs, Linux }`
- `LlamaServerArch.cs` — enum `{ X64, Arm64 }`
- `LlamaServerVariant.cs` — record `(string Build, LlamaServerBackend Backend, LlamaServerOs Os, LlamaServerArch Arch, string AssetName, long Bytes, string DownloadUrl, string? Sha256, string? CompanionAssetName, string? CompanionDownloadUrl, long? CompanionBytes, string? CompanionSha256)`
- `LlamaServerInstall.cs` — record `(string Id, LlamaServerSource Source, string? Build, LlamaServerBackend? Backend, string AbsolutePath, DateTimeOffset InstalledAt, bool IsValid)`
- `LlamaServerReleaseGroup.cs` — record `(string Build, IReadOnlyList<LlamaServerVariant> Variants)` — one GitHub release with N variants
- `LlamaServerInstallProgress.cs` — record `(string Phase, long BytesReceived, long? TotalBytes)` where `Phase ∈ { "downloading", "downloading-companion", "verifying", "extracting", "finalizing" }`
- `ILlamaServerCatalog.cs` — `Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchAsync(bool forceRefresh, CancellationToken ct)`
- `ILlamaServerInstaller.cs` — `Task<LlamaServerInstall> InstallAsync(LlamaServerVariant variant, IProgress<LlamaServerInstallProgress>? progress, CancellationToken ct)` and `Task UninstallAsync(string installId, CancellationToken ct)`
- `ILlamaServerRegistry.cs` — `Task<IReadOnlyList<LlamaServerInstall>> ListManagedAsync()`, `Task<LlamaServerInstall?> GetActiveAsync()`, `Task SetActiveAsync(string? installId, LlamaServerSource source)`
- `ILlamaCppServerLifecycle.cs` — `Task StopForReplacementAsync(CancellationToken ct)` — exposed by the recognizer so the installer can stop the active sidecar before deleting files (Windows file-lock).

### `src/Parlotype.Platform/Speech/LlamaServer/`

- `LlamaServerAssetParser.cs` — pure parser, unit-testable: `bool TryParse(string assetName, out LlamaServerVariant variant)` and `TryParseCompanion(...)` for cudart zips. Tolerant: unknown variants become `LlamaServerBackend.Unknown` and are skipped rather than crashing the catalog.
- `GitHubLlamaServerCatalog.cs` — implements `ILlamaServerCatalog`. `HttpClient` + `User-Agent: parlotype/{version}`, `If-None-Match` ETag, on-disk cache at `.cache/releases.json` valid 1h. Filters by `OperatingSystem.IsWindows()` + `RuntimeInformation.OSArchitecture`.
- `JsonLlamaServerRegistry.cs` — implements `ILlamaServerRegistry`. Reads/writes `manifest.json`. Thread-safe via `SemaphoreSlim` (mirror [JsonSettingsService.cs](../../src/Parlotype.Platform/Settings/JsonSettingsService.cs) pattern). On corrupt-file read, rename to `manifest.json.bak`, log warning, rebuild from folder scan.
- `LlamaServerInstaller.cs` — implements `ILlamaServerInstaller`. See Phase 4 for orchestration steps.
- `StreamingFileDownloader.cs` — small helper extracted from [HttpModelDownloadService.cs:38-97](../../src/Parlotype.Platform/Speech/HttpModelDownloadService.cs). Reused by both Whisper and llama installers. (Do not broaden `HttpModelDownloadService` itself — keep its blast radius small.)

### `src/Parlotype.Desktop/`

- `Services/LlamaServerInstallDialogService.cs` — Desktop-layer wrapper that opens the (generalized) modal download dialog and forwards progress, mirroring how `ModelDownloadDialogService` wraps `HttpModelDownloadService`. Implements `ILlamaServerInstaller` and delegates to the Platform `LlamaServerInstaller`.
- `ViewModels/Settings/LlamaCppSettingsViewModel.cs` — extended (see Phase 7).
- `Views/Settings/LlamaCppSettingsView.axaml` — reworked (see Phase 7).

### `Parlotype.Tests`

- `LlamaServerAssetParserTests` — fixture asset names → expected `LlamaServerVariant`. Covers cuda 12.4, cuda 13.1, vulkan, cpu x64, cpu arm64, sycl, hip-radeon, macOS arm64+kleidiai, ubuntu vulkan, cudart companions.
- `JsonLlamaServerRegistryTests` — round-trip manifest, list, set-active, handle missing/corrupt file.
- `LlamaServerInstallerTests` — uses a tiny fixture ZIP (committed under `Parlotype.Tests/Fixtures/`) served by an in-process `HttpListener`. Verifies atomic-rename, companion merge, SHA256-fail rollback, cancel-mid-download cleanup.
- `GitHubLlamaServerCatalogTests` — recorded JSON fixture from the real GitHub API; verifies ETag round-trip and tolerant parsing.

### `Parlotype.Desktop.Tests`

Add `Mocks/MockLlamaServerCatalog.cs`, `Mocks/MockLlamaServerInstaller.cs`, `Mocks/MockLlamaServerRegistry.cs`. Headless UI tests in `LlamaCppSettingsViewTests` for: listing, install command invocation, set-active flow, manual badge visibility.

---

## Reusable existing code

- [HttpModelDownloadService.cs:38-97](../../src/Parlotype.Platform/Speech/HttpModelDownloadService.cs) — download loop (atomic temp file, streaming chunks, progress, cancel). Extract into `StreamingFileDownloader` and share.
- [ModelDownloadDialog.axaml](../../src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml) + [ModelDownloadViewModel.cs](../../src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs) — generalize so title/subtitle come from the caller, not hard-coded to Whisper. Both Whisper and llama installs use it.
- [JsonSettingsService.cs](../../src/Parlotype.Platform/Settings/JsonSettingsService.cs) — pattern to mirror in `JsonLlamaServerRegistry` (SemaphoreSlim + read-modify-write).
- [LlamaCppServerInfo.cs](../../src/Parlotype.Platform/Speech/LlamaCppServerInfo.cs) — keep as-is; the new UI re-uses the probe.
- [PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs) — registration point for the four new services.

---

## Phases

### Phase 1 — Core contracts

Add the enums, records, and interfaces under `src/Parlotype.Core/Speech/LlamaServer/`. No implementations. `dotnet build` clean (zero warnings).

**Critical files (new):** all under `src/Parlotype.Core/Speech/LlamaServer/`.

### Phase 2 — Manifest + registry

Implement `JsonLlamaServerRegistry` in Platform. Unit tests for round-trip, set-active, missing-file recovery, corrupt-file recovery (rename to `.bak`, rebuild from folder scan).

**Critical files (new):** `src/Parlotype.Platform/Speech/LlamaServer/JsonLlamaServerRegistry.cs`, `src/Parlotype.Tests/Speech/LlamaServer/JsonLlamaServerRegistryTests.cs`.

### Phase 3 — GitHub catalog

Implement `LlamaServerAssetParser` + `GitHubLlamaServerCatalog`. ETag + `If-None-Match` + 1h on-disk cache. Tolerant parsing — unknown backends become `Unknown` and are filtered out at the catalog level (logged once). Filter results to Windows x64 in phase 1.

**Critical files (new):** `src/Parlotype.Platform/Speech/LlamaServer/LlamaServerAssetParser.cs`, `src/Parlotype.Platform/Speech/LlamaServer/GitHubLlamaServerCatalog.cs`, `src/Parlotype.Tests/Speech/LlamaServer/LlamaServerAssetParserTests.cs`, `src/Parlotype.Tests/Speech/LlamaServer/GitHubLlamaServerCatalogTests.cs`, `src/Parlotype.Tests/Fixtures/llama-cpp-releases.json` (recorded API response).

### Phase 4 — Installer

Implement `LlamaServerInstaller`. Orchestration:

1. Disk-space precheck (`DriveInfo.AvailableFreeSpace > assetBytes * 3`).
2. Download main asset to `.staging/{guid}/main.zip` via `StreamingFileDownloader`.
3. For CUDA variants: download companion (cudart) to `.staging/{guid}/companion.zip`. Failure → fail whole install.
4. SHA256 verify each downloaded file when `digest` was present in the manifest; log warning when absent.
5. Extract `.zip` via `System.IO.Compression.ZipFile.ExtractToDirectory` into `.staging/{guid}/payload/`. (`.tar.gz` extraction is stubbed with `NotSupportedException` and gated by `LlamaServerOs` — phase 1 is Windows-only.)
6. Extract companion zip into the same `payload/` directory (merge).
7. Atomic `Directory.Move` from `.staging/{guid}/payload/` to `llama-servers/{id}/`.
8. Update `manifest.json` via `ILlamaServerRegistry`.
9. On any failure: delete `.staging/{guid}/`.

Uninstall:
1. If `installId` matches active, call `ILlamaCppServerLifecycle.StopForReplacementAsync` first (file-lock release on Windows).
2. `Directory.Delete(path, recursive: true)`.
3. Update manifest.

Integration test with fixture ZIP and in-process `HttpListener`.

**Critical files (new):** `src/Parlotype.Platform/Speech/LlamaServer/LlamaServerInstaller.cs`, `src/Parlotype.Platform/Speech/StreamingFileDownloader.cs`, `src/Parlotype.Tests/Speech/LlamaServer/LlamaServerInstallerTests.cs`, `src/Parlotype.Tests/Fixtures/fake-llama-bin.zip`, `src/Parlotype.Tests/Fixtures/fake-cudart.zip`.
**Critical files (modified):** [src/Parlotype.Platform/Speech/HttpModelDownloadService.cs](../../src/Parlotype.Platform/Speech/HttpModelDownloadService.cs) (delegate download loop to `StreamingFileDownloader`).

### Phase 5 — Recognizer wiring

- Add `SettingsKeys.LlamaCppActiveInstall` to [SettingsKeys.cs](../../src/Parlotype.Core/Settings/SettingsKeys.cs).
- Modify `LlamaCppSpeechRecognizer.GetServerPathAsync` ([LlamaCppSpeechRecognizer.cs:223](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs)) to consult `ILlamaServerRegistry` when `LlamaCppActiveInstall` starts with `managed:`; otherwise fall back to existing logic.
- Implement `ILlamaCppServerLifecycle` on the recognizer (delegates to existing `UnloadAsync`).
- Tests: managed-active resolves to managed path; manual-active resolves to legacy path; missing-managed gracefully falls back.

**Critical files (modified):** [src/Parlotype.Core/Settings/SettingsKeys.cs](../../src/Parlotype.Core/Settings/SettingsKeys.cs), [src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs).

### Phase 6 — DI + dialog generalization

- Register the four new services in [PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs); register the Desktop wrapper in [App.axaml.cs](../../src/Parlotype.Desktop/App.axaml.cs).
- Generalize `ModelDownloadDialog` / `ModelDownloadViewModel` to accept caller-supplied title/subtitle/size; rename Whisper-specific fields to be generic. Existing Whisper download path keeps working via a thin adapter.
- Existing Whisper tests must still pass.

**Critical files (modified):** [src/Parlotype.Platform/PlatformServiceExtensions.cs](../../src/Parlotype.Platform/PlatformServiceExtensions.cs), [src/Parlotype.Desktop/App.axaml.cs](../../src/Parlotype.Desktop/App.axaml.cs), [src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml](../../src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml), [src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs](../../src/Parlotype.Desktop/ViewModels/ModelDownloadViewModel.cs).

### Phase 7 — Settings UI rework

Extend [LlamaCppSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/LlamaCppSettingsViewModel.cs) with:
- `ObservableCollection<LlamaServerInstallRowVm> Installed`
- `ObservableCollection<LlamaServerVariantRowVm> Available`
- `bool IsUpdateAvailable`, `string? LatestAvailableBuild`
- Commands: `InstallVariantCommand`, `UninstallInstallCommand`, `SetActiveCommand`, `CheckForUpdatesCommand`
- Existing port + probe controls preserved.

Rework [LlamaCppSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml) into sections:

- **Active server** (top): badge "Managed" or "Manual", build, backend, "Stop / Restart".
- **Installed**: managed entries as rich cards (build, backend chip, size, Uninstall button, "Set Active" radio).
- **Manual install** (separate panel, distinct background, badge "Manual — not managed by Parlotype"): existing folder picker + path display.
- **Available** (collapsible list): from catalog, filtered, "Install" button per row, size displayed in MB.
- **Check for updates** button + update banner.
- Existing port + probe controls preserved at the bottom.

Headless UI tests in `Parlotype.Desktop.Tests` using the new mocks.

**Critical files (modified):** [src/Parlotype.Desktop/ViewModels/Settings/LlamaCppSettingsViewModel.cs](../../src/Parlotype.Desktop/ViewModels/Settings/LlamaCppSettingsViewModel.cs), [src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml](../../src/Parlotype.Desktop/Views/Settings/LlamaCppSettingsView.axaml).
**Critical files (new):** `src/Parlotype.Desktop/Services/LlamaServerInstallDialogService.cs`, `src/Parlotype.Desktop/ViewModels/Settings/LlamaServerInstallRowVm.cs`, `src/Parlotype.Desktop/ViewModels/Settings/LlamaServerVariantRowVm.cs`, `src/Parlotype.Desktop.Tests/Mocks/MockLlamaServerCatalog.cs` (+ Installer + Registry), `src/Parlotype.Desktop.Tests/Settings/LlamaCppSettingsViewTests.cs`.

### Phase 8 — ADR + docs + memory vault

- **ADR-026: Managed llama.cpp server installation.** Storage layout, manifest schema, staging+rename, companion handling, catalog caching/ETag, SHA256 policy, Windows-only phase-1 scope. Notes that ADR-025's "manual folder" guidance is **augmented**, not superseded.
- Update [docs/architecture/llamacpp-subsystem.md](../../docs/architecture/llamacpp-subsystem.md) section 8 (Configuration Surface) and add new section "Server Installation Lifecycle".
- Update `memory/services/parlotype-platform.md` and `memory/services/parlotype-desktop.md` with the new symbols.
- Index the ADR in `memory/decisions/_index.md`.
- Add `memory/knowledge/llama-cpp-release-assets.md` capturing the asset naming convention, cudart pairing rule, `b{N}` versioning, and the 60 req/hr unauthenticated GitHub rate limit. These are non-derivable third-party facts.

**Critical files (new):** `docs/decisions/026-managed-llama-server-install.md`, `memory/knowledge/llama-cpp-release-assets.md`.
**Critical files (modified):** [docs/architecture/llamacpp-subsystem.md](../../docs/architecture/llamacpp-subsystem.md), `memory/services/parlotype-platform.md`, `memory/services/parlotype-desktop.md`, `memory/decisions/_index.md`.

---

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| GitHub unauthenticated rate limit (60/h) trips during dev | 1h cache + ETag `If-None-Match`; clear UI when 403-rate-limited; manual "Check for updates" button, no background polling. |
| Asset naming changes upstream | Tolerant parser: unknown backends → `Unknown`, logged-and-skipped, not fatal. |
| CUDA install missing cudart DLLs | Per-variant `CompanionAssetName` (not a global rule); both downloaded and extracted atomically into the same install folder. |
| Active server's files locked on Windows during uninstall/switch | `ILlamaCppServerLifecycle.StopForReplacementAsync` called before any `Directory.Delete`/`Move`. |
| Partial download/extract leaves garbage | All work happens in `.staging/{guid}/`; single `Directory.Move` commits; failure path deletes staging dir. |
| Disk full mid-install | Precheck `AvailableFreeSpace > assetBytes * 3` (download + extraction headroom). |
| Asset has no `digest` | Log a warning, install anyway. Manifest records `sha256: null`. |
| User edits `manifest.json` by hand | On read failure, rename to `manifest.json.bak`, rebuild from folder scan, log warning. |
| ZIP slip / path traversal | Validate every entry path in the ZIP resolves inside the staging dir before extracting (defensive, since `ZipFile.ExtractToDirectory` already does this in modern .NET, but worth an explicit check). |

---

## Definition of Done checklist

Per [CLAUDE.md](../../CLAUDE.md) §Definition of Done:

1. ☐ `dotnet build Parlotype.slnx` clean (zero warnings) and `dotnet test` green after every phase.
2. ☐ End-to-end manual verification on Windows (see [task.md](task.md) §Verification, steps 1-12).
3. ☐ ADR-026 written (Phase 8).
4. ☐ Memory vault updated: `memory/services/parlotype-platform.md`, `memory/services/parlotype-desktop.md`, `memory/decisions/_index.md`, `memory/knowledge/llama-cpp-release-assets.md` (Phase 8).
5. ☐ Non-derivable third-party facts captured under `memory/knowledge/llama-cpp-release-assets.md` (Phase 8).
