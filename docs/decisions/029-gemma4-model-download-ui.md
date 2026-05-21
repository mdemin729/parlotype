---
status: accepted
date: 2026-05-19
---

# 029. Gemma 4 model download UI (E2B + E4B)

## Context

The Gemma 4 (llama.cpp) engine requires a GGUF model file plus an mmproj
vision projector on disk. Until now exactly one variant was hardcoded
(`Gemma4ModelInfo.Default` = `E4B Q4_K_M`), and `LlamaCppSpeechRecognizer`
threw `"Download it first in Settings"` if the files were absent — but **no
Settings UI performed the download**. The only downloader,
`Gemma4ModelDownloadService`, was unreachable from the desktop app.

We want: a curated choice of variants (E2B and E4B), an in-app download with
a progress bar, and the recognizer honoring the user's choice.

### Surveyed alternative: delegate to llama-server `-hf`

llama-server natively downloads from HuggingFace via `-hf <repo>[:quant]`
(see `common/arg.cpp` in the llama.cpp source, `--hf-repo` / `--hf-file` /
`--hf-token`). It auto-discovers mmproj, caches to `~/.cache/huggingface/hub/`,
and supports gated repos via `HF_TOKEN`.

We **chose not to** delegate in v1: the first run would block while a ~6 GB
download happens inside the server process with no progress feedback in our
UI, and failures would surface only as server stderr. Our existing C#
downloader already streams with progress. The `-hf` route remains a viable
future fallback (e.g. for gated or long-tail repos).

## Decision

- **Catalog.** `Gemma4ModelInfo` is a record + static catalog with **five
  entries** (variant × quantization), keyed by a `ModelId` string, exposed via
  `All` and `GetById(string)`. Enums `Gemma4Variant { E2B, E4B }` and
  `Gemma4Quant { Q4_K_M, Q8_0, BF16 }`. `Default => E4B Q4_K_M`.

  | ModelId | GGUF | ~Total (with bf16 mmproj) |
  |---|---|---|
  | gemma-4-E2B-it-Q8_0 | E2B Q8_0 | ~5.5 GiB |
  | gemma-4-E2B-it-bf16 | E2B BF16 | ~9.6 GiB |
  | gemma-4-E4B-it-Q4_K_M | E4B Q4_K_M (default) | ~5.9 GiB |
  | gemma-4-E4B-it-Q8_0 | E4B Q8_0 | ~8.4 GiB |
  | gemma-4-E4B-it-bf16 | E4B BF16 | ~15 GiB |

  The `ggml-org/gemma-4-E2B-it-GGUF` repo publishes **no `Q4_K_M`** (only
  `Q8_0` and `bf16`), so there are 2 E2B + 3 E4B entries rather than a uniform
  set. All entries pair with the bf16 mmproj projector (small, highest
  quality, matches the known-good config). `BF16` models are offered for
  experimentation despite a known hallucination issue on some GPUs
  ([[gemma4-cuda-blackwell]]).
- **Selection.** Settings key `SelectedGemma4Model` stores the chosen
  `ModelId`. `LlamaCppSpeechRecognizer` resolves it via `GetById` at init,
  falling back to `Default`.
- **Downloader.** `Gemma4ModelDownloadService` methods take a
  `Gemma4ModelInfo` argument; added `DeleteModelAsync`. URL pattern unchanged
  (`https://huggingface.co/{repo}/resolve/main/{file}`), cache directory
  unchanged (`%LOCALAPPDATA%/parlotype/models`, filenames disambiguate
  variants).
- **UI.** New `Gemma4ModelSettingsViewModel` + `Gemma4ModelSettingsView`
  (Category `SpeechEngine`, `RestrictToEngine = Gemma4` — visible only when
  Gemma 4 is the active engine, per ADR-028). Lists both variants with an
  installed badge and inline Download/Delete buttons. Download reuses the
  existing `ModelDownloadDialog` via a new `Gemma4ModelDownloadDialogService`
  and a `ModelDownloadViewModel.ForGemma4Model` factory.
- **Quantization.** All quantizations each repo publishes are offered (5 total)
  so the user can benchmark accuracy/speed/size trade-offs. (Revised from an
  initial "Q4_K_M only" scope.)
- **Migration.** None — there is no detection of a previously-downloaded file;
  users download via the new UI.

### Explicitly out of scope

- HuggingFace token / gated-repo support (`ggml-org` mirrors are public).
- SHA256 verification (HF doesn't publish per-file SHAs for these repos).
- A generic "any HF model" catalog abstraction — three download subsystems
  (Whisper, llama-server binaries, Gemma models) already exist; a fourth
  abstraction without a second real consumer would be premature.
- Background pre-download.

## Consequences

**Easier**

- Users can pick any of the five variant×quant models (E2B faster, E4B more
  accurate; higher quant = better quality, larger) and download with progress,
  without leaving the app.
- Adding another model later is a one-record change in the catalog.
- The recognizer's "download it first" error is now actionable — the section
  that resolves it auto-appears when Gemma 4 is selected.
- Download dialog has explicit completion: on success it shows a single
  **Close** button (Download/Cancel hidden) with a success message, instead of
  auto-closing. Progress reports a single cumulative figure across the GGUF +
  mmproj pair (combined size discovered via HEAD) so the dialog total matches
  the list's disk-size label.

**Harder / trade-offs**

- "Only models supported by llama.cpp server" cannot be enforced statically
  (GGUF is necessary but not sufficient; architecture support is
  version-dependent). We rely on the curated catalog being tested against the
  supported server versions rather than runtime detection.
- Two parallel model-download UX paths now exist (Whisper auto-downloads on
  first use; Gemma requires an explicit Download click). This is intentional —
  a 6 GB silent download would be hostile — but it's an inconsistency to be
  aware of.
- Repo strings (`ggml-org/gemma-4-E{2,4}B-it-GGUF`) and exact filenames must
  match real HF assets; a wrong string fails at download time with a 404, not
  at build time. (Initial E2B record assumed `Q4_K_M` and 404'd in manual
  testing; the catalog was rebuilt from the repos' actual file lists verified
  via the HF API.) Quant coverage is therefore not uniform across variants —
  E2B has no Q4_K_M.
- Five entries means up to ~44 GiB if a user downloads everything. Acceptable
  for an experimentation feature; all share one cache directory and Delete
  reclaims space per model.

## Verification

- `dotnet build Parlotype.slnx -p:EnableCuda=false` — clean, zero warnings.
- `dotnet test` — Core 254, Desktop 108, Benchmark 95 all pass.
- New tests: `Gemma4ModelInfoTests` (5-entry catalog, `GetById`, `Default`),
  `Gemma4ModelDownloadServiceTests` (per-entry path helpers),
  `Gemma4ModelSettingsViewModelTests`, `Gemma4ModelSettingsScreenshotTests`;
  `SettingsWindowViewModelTests` updated to expect "Gemma 4 model" in the
  Gemma 4-active nav.
- Manual: E2B Q8_0 downloaded after fixing two bugs found in testing —
  (1) E2B `Q4_K_M` 404 (no such asset), (2) `File.Move` failing because the
  temp `FileStream` was still open (`await using var` disposes at method end;
  fixed by scoping the streams in a `using` block before the move).
