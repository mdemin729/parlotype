---
type: knowledge
tags: [llamacpp, gemma4, huggingface, download]
created: 2026-05-19
summary: llama-server can download HF models itself via -hf; we deliberately use our own C# downloader for progress UX
---

# llama-server built-in HuggingFace download

## Fact

`llama-server` (llama.cpp) downloads GGUF models directly from HuggingFace
when given `-hf <user>/<repo>[:quant]`. Verified in the llama.cpp source at
`common/arg.cpp` (around line 2620):

- `-hf` / `-hfr` / `--hf-repo` — `<user>/<model>[:quant]`. Quant is optional,
  case-insensitive, **defaults to `Q4_K_M`**, falls back to the first file in
  the repo if that quant is absent. **mmproj is auto-downloaded** if present;
  disable with `--no-mmproj`.
- `-hff` / `--hf-file` — override the specific file.
- `-hft` / `--hf-token` (or `HF_TOKEN` env var) — access token for gated repos.
- Cache location resolves via `HF_HOME` → `XDG_CACHE_HOME/huggingface/hub` →
  `~/.cache/huggingface/hub` (`common/hf-cache.cpp`). Interoperable with the
  `huggingface_hub` Python tooling.

## Why we don't use it (as of ADR-029)

Parlotype downloads Gemma 4 GGUFs with its own `Gemma4ModelDownloadService`
(C#, streamed with `IProgress`) rather than delegating to `-hf`, because:

- The first `-hf` run blocks inside the server process while a ~6 GB file
  downloads, with **no progress reporting** surfaced to our UI.
- Download failures would surface only as server stderr, not as typed
  exceptions we can show in a dialog.

`-hf` remains a sensible **future fallback** for gated repos (needs
`HF_TOKEN`) or long-tail models we don't want to curate. If we ever add it,
the cache layout requires resolving the `main` ref to a commit OID via the HF
API before files land in the standard hub layout — our current downloader
sidesteps this by writing to `%LOCALAPPDATA%/parlotype/models` and passing
`-m`/`--mmproj` explicitly.
