---
title: llama-server Props Endpoint & Gemma 4 GGUF Naming
type: knowledge
tags: [llamacpp, gemma4, gguf, api]
created: 2026-05-09
summary: llama-server /props endpoint for identification, and correct Gemma 4 E4B GGUF filenames on HuggingFace
---

# llama-server Props Endpoint & Gemma 4 GGUF Naming

## `/props` for server identification

llama-server exposes a `/props` endpoint that returns server metadata (model alias, model path, modalities, build info). This endpoint is **llama-server-specific** — other servers (Python FastAPI sidecars, LM Studio, Ollama) do not have it. Use `/props` after `/health` to confirm that a process on a port is actually llama-server and not another application.

## Gemma 4 E4B GGUF filenames (ggml-org)

The canonical HuggingFace repo is `ggml-org/gemma-4-E4B-it-GGUF`. Filenames are **case-sensitive**:

| File | Size | Notes |
|------|------|-------|
| `gemma-4-E4B-it-Q4_K_M.gguf` | 4.97 GB | Main model (Q4 quantization) |
| `mmproj-gemma-4-E4B-it-bf16.gguf` | 0.92 GB | Multimodal projector (audio + vision) |

Common mistake: the mmproj uses `bf16` (bfloat16), not `f16` (float16). The naming convention uses uppercase `E4B` and hyphens, matching the HuggingFace filenames exactly.

## llama-server audio performance (Vulkan, RTX 5070 Ti)

- Cold start: ~10-15 seconds for Gemma 4 E4B Q4 on NVMe + Vulkan
- Prompt processing (audio encoding): ~5-10 seconds for a short clip
- Token generation: ~30-40 ms/token (fast)
- `--flash-attn on` and `--jinja` flags required for audio support
- `--mmproj` flag is mandatory for audio; without it, server returns "audio input is not supported"
