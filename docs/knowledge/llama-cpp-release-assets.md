---
title: "llama.cpp GitHub Release Asset Conventions"
area: "speech-engines"
summary: "llama.cpp binaries live in b{N} releases that are now flagged pre-release, v{X.Y.Z} releases carry only a pointer file, Windows CUDA builds need a matching cudart zip, and unauthenticated GitHub API calls cap at 60/h."
verified: 2026-11-13
---

# llama.cpp GitHub Release Asset Conventions

These are non-derivable facts about how the
[`ggml-org/llama.cpp`](https://github.com/ggml-org/llama.cpp) project
publishes binary releases. They underpin
[ADR-026](../decisions/026-managed-llama-server-install.md) (managed llama-server install)
and the parser in
[`LlamaServerAssetParser.cs`](../../src/Parlotype.Platform/LlamaServer/LlamaServerAssetParser.cs).

## Versioning: `b{N}` builds are pre-releases, `v{X.Y.Z}` has no binaries

Binaries are published in releases tagged `b{N}`, where `N` is the master
commit count. Example: `b12360`. Ten to twenty land per day.

**The `prerelease` flag changed meaning in late 2026.** `b10330` (August
2026) is a normal release; `b12000` (30 October 2026) and everything after
it is flagged `prerelease: true`. Upstream now calls the `b{N}` builds
nightlies and uses the non-pre-release slot for versioned releases:
`v1.0.0` was published on 2026-11-13 (after a `v0.9.0`). A versioned
release has **no binaries**. Its single asset is `nightly-tag.txt`, seven
bytes naming the build it corresponds to (`b12359` for `v1.0.0`), and its
notes link the same build. Upstream's explanation is in
[ggml discussion 1579](https://github.com/ggml-org/ggml/discussions/1579).

Consequences:

- **Do not filter on `prerelease`.** A catalog that skips pre-releases sees
  no installable asset at all. Parlotype's did, and showed an empty
  "Available builds" list from the day the flag changed until it was fixed
  on 2026-11-13. Skip drafts only.
- `GET /releases/latest` now answers with the versioned release
  (`v1.0.0`), which is useless for finding a download.
- The build a versioned release names drops off the first page of
  `/releases` within hours. Offering "the stable build" means reading
  `nightly-tag.txt` (or the release notes) and then fetching
  `/releases/tags/b{N}`: two more requests, which Parlotype does not make.
  It lists the newest builds, as it always has.

## Asset naming pattern (main archives)

`llama-{build}-bin-{platform}-{backend}[{-variant}]-{arch}.{ext}` where:

| Field | Examples |
|-------|----------|
| `build` | `b9198` |
| `platform` | `win`, `macos`, `ubuntu`, `android`, `310p`, `910b` |
| `backend` | `cpu`, `cuda`, `vulkan`, `sycl`, `hip` (+ `radeon` variant, older builds), `rocm`, `openvino`, `opencl-adreno`, `kleidiai` |
| `arch` | `x64`, `arm64`, `aarch64`, `x86`, `s390x` |
| `ext` | `.zip` (Windows), `.tar.gz` (everything else) |

CUDA variants include the toolkit minor:
`llama-b9198-bin-win-cuda-12.4-x64.zip` / `-cuda-13.1-x64.zip`. The
`LlamaServerBackend` enum collapses these to `Cuda12` / `Cuda13`;
preserve the minor by extracting from the asset name when needed
(`LlamaServerInstaller.BuildInstallId` does this for the install id).

## CUDA companion (`cudart-*.zip`) — Windows only

CUDA-on-Windows builds ship the backend DLL (`ggml-cuda.dll`) without
the NVIDIA runtime libraries. Users who don't have the CUDA Toolkit
installed need a **separate** `cudart-llama-bin-win-cuda-{version}-x64.zip`
archive containing `cudart64_*.dll`, `cublas64_*.dll`, `cublasLt64_*.dll`
extracted into the same folder as `llama-server.exe`.

Important pairing rules:

- The cudart filename **does not include the build number** (`cudart-llama-bin-win-cuda-12.4-x64.zip`,
  not `cudart-llama-b9198-bin-win-cuda-12.4-x64.zip`). Within one
  release, pair by `(cudaVersion, arch)`.
- The CUDA version on the cudart archive **must match** the main
  archive's CUDA version. 12.4 cudart with a 13.1 build does not work.
- No companion is needed for any non-CUDA-Windows backend (Vulkan,
  SYCL, HIP, CPU, all macOS, all Linux).

`LlamaServerVariant.CompanionAssetName` / `CompanionDownloadUrl` /
`CompanionBytes` / `CompanionSha256` carry this pairing per-variant —
the catalog populates them when the variant is CUDA-Windows, the
installer fetches+extracts the companion into the same install folder
when present.

## What's inside a Windows ZIP

The CI's pack step is `7z a -snl llama-bin-win-*-x64.zip .\build\bin\Release\*`,
so a Windows archive contains the flat contents of the build output:
`llama-server.exe`, `llama-cli.exe`, supporting DLLs (`libomp140.x64.dll`,
backend-specific `ggml-*.dll`), and `LICENSE`. There is **no wrapping
top-level folder** — `ZipFile.ExtractToDirectory` produces a flat
install folder.

## Tolerant parsing

llama.cpp has historically renamed asset variants (`win-noavx`,
`kompute`, etc.). Parlotype's parser:

- Reports unknown backend strings as `LlamaServerBackend.Unknown` and
  unknown OS strings as `LlamaServerOs.Unknown` instead of throwing —
  the catalog filters those out at the boundary.
- Returns `false` only for structurally invalid names (`source.zip`,
  bad build tag like `bnonsense`, wrong extension, too few segments).

This lets a new llama.cpp release with a renamed variant degrade
gracefully (the variant is hidden, the rest of the release still works)
instead of crashing the catalog.

## GitHub API rate limit (60 req/h unauthenticated)

The unauthenticated GitHub REST API limit is **60 requests per hour per
IP**. Parlotype talks to `/repos/ggml-org/llama.cpp/releases` without a
PAT, so this limit applies. Mitigations in `GitHubLlamaServerCatalog`:

- **1 h on-disk cache** (`.cache/releases.json`): within TTL, no HTTP
  is issued.
- **ETag (`If-None-Match`)**: stale TTL triggers a conditional GET; 304
  Not Modified extends the cache `fetchedAt` and costs 0 against the
  rate limit *only* on responses without bodies (304 still counts as 1
  request, but the cost amortizes since the body is reused).
- **No background polling**: the catalog is refreshed only on settings-
  page open and on the explicit "Check for updates" button click.

> **Note: ETag preservation.** `HttpHeaders.ETag.Tag` strips the `W/` weak-validator prefix. Sending
> a stripped ETag back violates RFC 7232's byte-exact echo requirement,
> so the catalog uses a tiny `FormatETag` helper to re-prepend `W/`
> when the validator was weak. Without that, servers may return 200
> with a full body instead of 304.

## The cache stores parsed results, so parser changes must invalidate it

`.cache/releases.json` holds the list *after* parsing, next to the response's
ETag. If the parsing rules change, the old list would be revalidated with
`If-None-Match`, answered with 304, and served again for as long as upstream
is unchanged. `GitHubLlamaServerCatalog.CacheVersion` is written into the
file and a cache with another version is ignored. Bump it whenever a change
alters which releases or assets are kept.

Newer builds also publish assets the parser rejects as not being server
archives, which is correct: `llama-b{N}-ui.tar.gz`, `llama-b{N}-xcframework.zip`,
and Linux CUDA runtime archives named `cudart-llama-b{N}-bin-ubuntu-cuda-…tar.gz`
(unlike the Windows companion, these carry the build number).

## All supported backends per platform (as of b12360)

For completeness — what the parser recognises today:

| Platform | Backends |
|----------|----------|
| Windows | `cpu` (x64, arm64), `cuda-{version}` (x64, arm64), `vulkan` (x64, arm64), `sycl` (x64), `rocm-{version}` (x64; `hip-radeon` before the rename, both map to `Hip`). `openvino-{version}` and `opencl-adreno` parse as `Unknown` and are hidden. |
| macOS | `arm64` (Metal), `arm64-kleidiai` (KleidiAI CPU), `x64` (CPU) |
| Ubuntu/Linux | bare arch (CPU), `vulkan`, `rocm-{version}`, `sycl-fp32`/`-fp16`, `openvino-{version}` |
| Android | `arm64` (CPU only) — recognised as `LlamaServerOs.Unknown` and filtered out |
| openEuler (CANN) | `310p` / `910b` — recognised as `LlamaServerOs.Unknown` and filtered out |

## Sources

- Release workflow: [`.github/workflows/release.yml`](https://github.com/ggml-org/llama.cpp/blob/master/.github/workflows/release.yml)
- Build-number action: [`.github/actions/get-tag-name/action.yml`](https://github.com/ggml-org/llama.cpp/blob/master/.github/actions/get-tag-name/action.yml)
- Recorded API responses (test fixtures):
  [`llama-cpp-releases.json`](../../src/Parlotype.Tests/Fixtures/llama-cpp-releases.json) (hand-built, the old scheme) and
  [`llama-cpp-releases-2026-11.json`](../../src/Parlotype.Tests/Fixtures/llama-cpp-releases-2026-11.json)
  (the live response of 2026-11-13, trimmed to `v1.0.0` and two builds)
