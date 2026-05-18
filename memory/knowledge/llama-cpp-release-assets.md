---
title: llama.cpp GitHub Release Asset Conventions
type: knowledge
tags: [llamacpp, github, releases, packaging, cuda]
created: 2026-05-17
last_updated: 2026-05-18
summary: Asset naming pattern, cudart pairing rule, b{N} versioning, and unauthenticated GitHub API rate limit relevant to Parlotype's managed-install installer
---

# llama.cpp GitHub Release Asset Conventions

These are stable, non-derivable facts about how the
[`ggml-org/llama.cpp`](https://github.com/ggml-org/llama.cpp) project
publishes binary releases. They underpin
[ADR-026](../decisions/_index|ADR-026) (managed llama-server install)
and the parser in
[`LlamaServerAssetParser.cs`](../../src/Parlotype.Platform/LlamaServer/LlamaServerAssetParser.cs).

## Versioning: `b{N}` tags, no "latest" alias

llama.cpp releases are tagged `b{N}` where `N` is the master commit
count at the time of release. Example: `b9198`. Several releases land
per week. There is **no `latest` alias** — consumers must query
`/repos/ggml-org/llama.cpp/releases` (newest-first by `published_at`)
and pick the head.

## Asset naming pattern (main archives)

`llama-{build}-bin-{platform}-{backend}[{-variant}]-{arch}.{ext}` where:

| Field | Examples |
|-------|----------|
| `build` | `b9198` |
| `platform` | `win`, `macos`, `ubuntu`, `android`, `310p`, `910b` |
| `backend` | `cpu`, `cuda`, `vulkan`, `sycl`, `hip` (+ `radeon` variant), `rocm`, `openvino`, `kleidiai` |
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

> [!note] ETag preservation
> `HttpHeaders.ETag.Tag` strips the `W/` weak-validator prefix. Sending
> a stripped ETag back violates RFC 7232's byte-exact echo requirement,
> so the catalog uses a tiny `FormatETag` helper to re-prepend `W/`
> when the validator was weak. Without that, servers may return 200
> with a full body instead of 304.

## All supported backends per platform (as of b9198)

For completeness — what the parser recognises today:

| Platform | Backends |
|----------|----------|
| Windows | `cpu` (x64, arm64), `cuda-{version}` (x64), `vulkan` (x64), `sycl` (x64), `hip-radeon` (x64) |
| macOS | `arm64` (Metal), `arm64-kleidiai` (KleidiAI CPU), `x64` (CPU) |
| Ubuntu/Linux | bare arch (CPU), `vulkan`, `rocm-{version}`, `sycl-fp32`/`-fp16`, `openvino-{version}` |
| Android | `arm64` (CPU only) — recognised as `LlamaServerOs.Unknown` and filtered out |
| openEuler (CANN) | `310p` / `910b` — recognised as `LlamaServerOs.Unknown` and filtered out |

## Sources

- Release workflow: [`.github/workflows/release.yml`](https://github.com/ggml-org/llama.cpp/blob/master/.github/workflows/release.yml)
  (local clone: `C:\projects\ggml-org\llama.cpp\.github\workflows\release.yml`)
- Build-number action: [`.github/actions/get-tag-name/action.yml`](https://github.com/ggml-org/llama.cpp/blob/master/.github/actions/get-tag-name/action.yml)
- Recorded API response (test fixture):
  [`src/Parlotype.Tests/Fixtures/llama-cpp-releases.json`](../../src/Parlotype.Tests/Fixtures/llama-cpp-releases.json)
