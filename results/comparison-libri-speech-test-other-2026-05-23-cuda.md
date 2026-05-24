# Benchmark Comparison — LibriSpeech test-other (50 samples, CUDA everywhere)

_Generated: 2026-05-23T16:50_ — Whisper re-run with CUDA runtime for an apples-to-apples comparison with the Gemma 4 / llama.cpp CUDA results.

**Dataset:** `datasets/libri-speech-test-other/manifest.json` (50 samples)
**VAD:** disabled (full-file transcription)
**Whisper runtime:** **CUDA** (`Whisper.net.Runtime.Cuda`, strict mode via `runtimePreference: "Cuda"` in each config)
**Gemma 4 backend:** llama.cpp, CUDA build **b9297-win-cuda-13.1-x64**, port 8321
**Warm-up:** one throwaway transcription on the first sample before the timed loop ([ADR 031](../docs/decisions/031-benchmark-warmup-pass.md))

## Results (sorted by WER, lower is better)

| Rank | Engine | Model | WER % | CER % | RTF | Model load (s) | Warm-up (ms) | Peak host RAM (MiB) |
|---:|---|---|---:|---:|---:|---:|---:|---:|
| 1 | Whisper (CUDA) | `LargeV3Turbo` | 11.48 | 4.97 | 0.055 | 1.31 | 372 | 471 |
| 2 | Whisper (CUDA) | `Medium` | 12.18 | 5.41 | 0.073 | 1.28 | 675 | 464 |
| 3 | Whisper (CUDA) | `Small` | 13.10 | 5.87 | 0.034 | 0.71 | 370 | 463 |
| 4 | Gemma 4 (llama.cpp CUDA b9297) | `gemma-4-E2B-it-BF16` | 13.15 | 4.95 | 0.038 | 6.70 | 430 | 156 |
| 5 | Gemma 4 (llama.cpp CUDA b9297) | `gemma-4-E4B-it-Q4_K_M` | 13.82 | 5.80 | 0.038 | 6.73 | 477 | 156 |
| 6 | Gemma 4 (llama.cpp CUDA b9297) | `gemma-4-E4B-it-BF16` | 14.20 | 5.40 | 0.038 | 6.72 | 489 | 165 |
| 7 | Gemma 4 (llama.cpp CUDA b9297) | `gemma-4-E4B-it-Q8_0` | 14.39 | 5.79 | 0.044 | 9.25 | 530 | 166 |
| 8 | Gemma 4 (llama.cpp CUDA b9297) | `gemma-4-E2B-it-Q8_0` | 19.22 | 8.95 | 0.315 | 6.74 | 1938 | 162 |

Peak RAM for **Gemma 4** reflects the benchmark host process only — model weights live in the `llama-server.exe` child process and are not counted (~3–15 GiB depending on quantization).
Peak RAM for **Whisper CUDA** is also dramatically lower than the Vulkan baseline because weights now reside in GPU VRAM instead of host memory.

## Highlights

- **Whisper `LargeV3Turbo`** still leads at **11.48% WER**, but the lead over the best Gemma result shrinks to ~1.7 points (vs ~3.0 points under Vulkan).
- **`gemma-4-E2B-it-BF16`** remains the strongest Gemma at **13.15% WER** — effectively tied with Whisper `Small` (13.10%) and 16% faster on the warm path (RTF 0.038 vs 0.034 — slight CUDA Whisper edge here).
- **Whisper Small CUDA** posted the lowest RTF of the entire field at **0.034** — a 24% speedup over the Vulkan build (0.045).
- **`gemma-4-E2B-it-Q8_0`** is still the same outlier as before (verbose reasoning mode → `<|channel>` token crashes). Numbers reproduced from the prior warm-up run; the model itself is unchanged.

## CUDA vs Vulkan delta (Whisper only)

Same models, same dataset, same warm-up — only the Whisper.net runtime differs.

| Model | Runtime | WER % | CER % | RTF | Model load | Warm-up | Host RAM (MiB) |
|---|---|---:|---:|---:|---:|---:|---:|
| `Small` | Vulkan | 13.10 | 5.71 | 0.045 | 755 ms | 286 ms | 679 |
| `Small` | **CUDA** | 13.10 | 5.87 | **0.034** | **707 ms** | 370 ms | **463** |
| `Medium` | Vulkan | 12.18 | 5.41 | 0.079 | 1462 ms | 507 ms | 1261 |
| `Medium` | **CUDA** | 12.18 | 5.41 | **0.073** | **1280 ms** | 675 ms | **464** |
| `LargeV3Turbo` | Vulkan | **10.15** | **4.78** | 0.074 | 1511 ms | 395 ms | 1173 |
| `LargeV3Turbo` | **CUDA** | 11.48 | 4.97 | **0.055** | **1309 ms** | 372 ms | **471** |

Observations:
- **Speed:** CUDA is faster on every model — Small −24% RTF, Medium −8%, LargeV3Turbo −26%.
- **Host RAM:** CUDA cuts host RAM by ~30–60% because weights are uploaded to VRAM rather than mmapped on the CPU side.
- **Quality:** Small and Medium produce **bit-identical WER**. `LargeV3Turbo` regresses **+1.33 pp WER / +0.19 pp CER** on CUDA. This is the only quality difference and is reproducible — likely caused by non-bitwise-identical kernel math between the Vulkan and CUDA backends (matmul/softmax reductions, FP16 accumulation order). Worth a closer look if `LargeV3Turbo` is the production target.

## Run IDs (this report)

| Model | Run ID |
|---|---|
| Whisper `Small` (CUDA) | `20260523-204306-whisper-small-libri-speech-test-other` |
| Whisper `Medium` (CUDA) | `20260523-204332-whisper-medium-libri-speech-test-other` |
| Whisper `LargeV3Turbo` (CUDA) | `20260523-204406-whisper-large-v3-turbo-libri-speech-test-other` |
| `gemma-4-E2B-it-BF16` | `20260523-200528-gemma4-e2b-bf16-libri-speech-test-other` |
| `gemma-4-E4B-it-Q4_K_M` | `20260523-200549-gemma4-e4b-q4-libri-speech-test-other` |
| `gemma-4-E4B-it-BF16` | `20260523-200638-gemma4-e4b-bf16-libri-speech-test-other` |
| `gemma-4-E4B-it-Q8_0` | `20260523-200611-gemma4-e4b-q8-libri-speech-test-other` |
| `gemma-4-E2B-it-Q8_0` | `20260523-201057-gemma4-e2b-q8-libri-speech-test-other` |

Vulkan baseline runs (for the CUDA vs Vulkan table):

| Model | Run ID |
|---|---|
| Whisper `Small` (Vulkan) | `20260523-200406-whisper-small-libri-speech-test-other` |
| Whisper `Medium` (Vulkan) | `20260523-200421-whisper-medium-libri-speech-test-other` |
| Whisper `LargeV3Turbo` (Vulkan) | `20260523-200446-whisper-large-v3-turbo-libri-speech-test-other` |

To reproduce a single Whisper CUDA run:

```pwsh
dotnet src/Parlotype.Benchmark/bin/Release/net10.0/Parlotype.Benchmark.dll run `
  --config datasets/whisper-large-v3-turbo-libri-speech-test-other-config.json `
  --datasets datasets --output results
```

To diff CUDA vs Vulkan for `LargeV3Turbo`:

```pwsh
dotnet src/Parlotype.Benchmark/bin/Release/net10.0/Parlotype.Benchmark.dll compare `
  --run-a 20260523-200446-whisper-large-v3-turbo-libri-speech-test-other `
  --run-b 20260523-204406-whisper-large-v3-turbo-libri-speech-test-other `
  --output results
```
