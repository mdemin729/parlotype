---
title: Knowledge Base
type: index
status: active
last_updated: 2026-05-24
summary: Semantic memory — stable facts learned across sessions that are not derivable from code
---

# Knowledge Base

This directory stores **stable facts** learned across sessions — things that are not obvious from reading the code alone.

## How to Add Knowledge
1. Create a new `.md` file in this directory with YAML frontmatter
2. Include: `type: knowledge`, `tags`, `created`, `summary`
3. Add an entry to this index

## Entries

<!-- Add entries as facts are learned across sessions -->
<!-- Format: | [[filename]] | one-line summary | date | -->

| Fact | Summary | Learned |
|------|---------|---------|
| [[whisper-net-quirks]] | Whisper.net 1.9.0 NuGet `CudaHelper` differs from upstream master; `WhisperLogLevel` enum is inverted vs native ggml | 2026-04-28 |
| [[agent-skills]] | Claude/Copilot skills require `.claude/skills/<name>/SKILL.md`; description-triggered discovery does not reliably fire at session boundaries, so per-session protocols belong in CLAUDE.md | 2026-04-30 |
| [[avalonia-devtools]] | Classic `Avalonia.Diagnostics` retired in 12; replacement is `AvaloniaUI.DiagnosticsSupport` (in-app) + `AvaloniaUI.DeveloperTools` (`avdt` global tool); free Essentials tier needs portal signup. Build telemetry: `AvaloniaStatsTask` POSTs hashed build metadata to `av-build-tel-api-v1.avaloniaui.net`; Community tier cannot opt out; no runtime telemetry. Set `AVALONIA_TELEMETRY_OPTOUT=1` when upgrading to paid tier. | 2026-04-30 |
| [[asyncrelaycommand-flicker]] | CommunityToolkit.Mvvm `AsyncRelayCommand` disables all buttons sharing the command while executing; use sync `RelayCommand` + fire-and-forget for shared commands in `ItemsControl` | 2026-04-30 |
| [[benchmark-pipeline-recommendations]] | Optimal STT settings from 234+ config sweep: Medium/en/beam=1/temp=0.0/no-VAD for accuracy; BaseEn for speed. language="en" gives 2× speedup; higher beam sizes never help | 2026-05-01 |
| [[vad-silence-threshold-constraint]] | Sub-500ms silence thresholds cause 77%+ WER. WaitTimeOption minimum is now Medium (500ms). Instant/VeryShort/Short removed. PipelineSimulator added for benchmark testing. | 2026-05-02 |
| [[sharphook-suppress-event]] | SharpHook `SuppressEvent` only works with `SimpleGlobalHook`; `TaskPoolGlobalHook` and `EventLoopGlobalHook` silently ignore it | 2026-05-02 |
| [[whisper-translation-models]] | Whisper translation to English only works with multilingual models (Medium+); English-only (*En) models don't support it; Base/Small produce mixed results | 2026-05-03 |
| [[vulkan-runtime-probing]] | Vulkan API version packing (variant/major/minor/patch bits), `VkPhysicalDeviceProperties` 824-byte struct with stable 276-byte head, and `RuntimeOptions.LoadedLibrary` semantics in Whisper.net | 2026-05-06 |
| [[gemma4-cuda-blackwell]] | Gemma 4 E2B bfloat16/float16 hallucinates on CUDA with Blackwell GPUs (RTX 5070 Ti, compute 12.0); CPU works; bitsandbytes 4-bit crashes on audio encoder `torch.finfo()` | 2026-05-08 |
| [[llamacpp-gemma4-integration]] | llama-server `/props` endpoint for server identification; Gemma 4 E4B GGUF filenames (case-sensitive `bf16` not `f16`); Vulkan audio performance on RTX 5070 Ti | 2026-05-09 |
| [[llama-cpp-release-assets]] | llama.cpp release asset naming (`llama-b{N}-bin-{platform}-{backend}-{arch}.{ext}`), CUDA `cudart-*.zip` companion pairing rules, no "latest" alias, GitHub 60 req/h unauthenticated limit, ETag weak-validator round-trip rule | 2026-05-17 |
| [[llama-server-hf-download]] | llama-server downloads HF models itself via `-hf <repo>[:quant]` (defaults Q4_K_M, auto-mmproj, `HF_TOKEN` for gated, `~/.cache/huggingface/hub`); Parlotype uses its own C# downloader instead for progress UX (ADR-029) | 2026-05-19 |
| [[whisper-cuda-runtime-packaging]] | `Whisper.net.Runtime.Cuda` adds only `ggml-cuda-whisper.dll` (~150 MB) to published output and does NOT bundle cudart/cublas — Full build still needs the user's installed CUDA toolkit. Self-contained sizes: Lite ~720 MB, Full ~870 MB unzipped (ADR-031) | 2026-05-24 |

## Distillation Rules
- Only store facts that are **not derivable** from reading current code or git history
- Include the "why" — reasoning, context, constraints
- Update or remove entries when they become stale
- Prefer specific, actionable facts over vague observations
