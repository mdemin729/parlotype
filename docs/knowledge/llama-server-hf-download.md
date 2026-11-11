---
title: "llama-server quirks (HF download, /props, audio flags)"
area: "speech-engines"
summary: "/props identifies llama-server, audio needs --mmproj (the projector is bf16, not f16), and -hf downloads models itself but reports no progress."
verified: 2026-11-11
---

# llama-server quirks (HF download, /props, audio flags)

## `/props` identifies llama-server

llama-server exposes a `/props` endpoint (model alias, model path, modalities, build info).
It is **llama-server-specific**: other servers that might hold the port (LM Studio, Ollama,
a Python sidecar) do not have it. Probe `/props` after `/health` to confirm the process on
a port really is llama-server. `LlamaCppServerInfo` does this.

## Audio needs `--mmproj`, and the projector is `bf16`

- Without `--mmproj` the server answers "audio input is not supported", not a startup error.
- The Gemma 4 multimodal projector on HuggingFace is named `mmproj-gemma-4-E4B-it-bf16.gguf`
  (bfloat16), **not** `f16`. HuggingFace filenames are case-sensitive (`E4B`, not `e4b`).
  The canonical names live in `Gemma4ModelInfo`.

## Built-in HuggingFace download (`-hf`)

`llama-server` downloads GGUF models directly from HuggingFace when given
`-hf <user>/<repo>[:quant]`. Verified in the llama.cpp source at `common/arg.cpp` (around
line 2620):

- `-hf` / `-hfr` / `--hf-repo`: `<user>/<model>[:quant]`. Quant is optional,
  case-insensitive, **defaults to `Q4_K_M`**, falls back to the first file in the repo if
  that quant is absent. **mmproj is auto-downloaded** if present; disable with `--no-mmproj`.
- `-hff` / `--hf-file`: override the specific file.
- `-hft` / `--hf-token` (or `HF_TOKEN` env var): access token for gated repos.
- Cache location resolves via `HF_HOME` → `XDG_CACHE_HOME/huggingface/hub` →
  `~/.cache/huggingface/hub` (`common/hf-cache.cpp`). Interoperable with the
  `huggingface_hub` Python tooling.

### Why Parlotype does not use it

Parlotype downloads Gemma 4 GGUFs with its own `Gemma4ModelDownloadService` (C#, streamed
with `IProgress`) rather than delegating to `-hf`
([ADR-029](../decisions/029-gemma4-model-download-ui.md)), because:

- The first `-hf` run blocks inside the server process while a ~6 GB file downloads, with
  **no progress reporting** surfaced to the UI.
- Download failures would surface only as server stderr, not as typed exceptions a dialog
  can show.

`-hf` remains a sensible **future fallback** for gated repos (needs `HF_TOKEN`) or long-tail
models not worth curating. If it is ever added, the cache layout requires resolving the
`main` ref to a commit OID via the HF API before files land in the standard hub layout. The
current downloader sidesteps this by writing to `%LOCALAPPDATA%/parlotype-data/models` and
passing `-m`/`--mmproj` explicitly.
