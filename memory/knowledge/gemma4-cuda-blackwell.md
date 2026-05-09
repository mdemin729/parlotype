---
title: Gemma 4 CUDA Inference on Blackwell GPUs
type: knowledge
tags: [gemma4, cuda, blackwell, rtx5070ti, precision]
created: 2026-05-08
summary: Gemma 4 E2B produces garbage transcriptions on CUDA with bfloat16/float16 on Blackwell GPUs (compute 12.0). CPU inference works correctly. bitsandbytes 4-bit quantization crashes on audio encoder.
---

# Gemma 4 CUDA Inference on Blackwell GPUs

## Problem

Gemma 4 E2B (`google/gemma-4-E2B-it`) produces hallucinated/garbage transcriptions when running on CUDA with either `bfloat16` or `float16` dtype on an NVIDIA RTX 5070 Ti (Blackwell architecture, compute capability 12.0).

- **bfloat16 on CUDA**: 90.3% WER (vs 11.7% on CPU) — model hallucinates plausible but incorrect text
- **float16 on CUDA**: 96.9% WER — same issue
- **float32 on CUDA**: doesn't fit in 16 GB VRAM (~20 GB required)
- **bfloat16 on CPU**: 11.7% WER — correct transcriptions

## bitsandbytes 4-bit Quantization

`bitsandbytes` 4-bit quantization crashes during inference on Gemma 4's audio encoder:

```
TypeError: torch.finfo() requires a floating point input type. Use torch.iinfo to handle 'torch.uint32'
```

Location: `transformers/models/gemma4/modeling_gemma4.py:427` — `gradient_clipping` accesses `ffw_layer_1.linear.weight.dtype` which is `uint32` in quantized mode.

## Environment

- PyTorch 2.11.0+cu128
- transformers >= 4.52.0
- NVIDIA GeForce RTX 5070 Ti (16 GB VRAM, compute capability 12.0)
- CUDA 12.8
- Windows 10

## Workaround

Use CPU inference (`device_map="cpu"`) with `torch_dtype=torch.bfloat16`. RTF is ~0.6 (acceptable for benchmarking, not real-time).

## Status

Likely a `transformers` or PyTorch compatibility issue with Blackwell architecture. Monitor future releases.
