---
status: accepted
date: 2026-07-31
supersedes: [012]
amends: [014, 022, 031]
---

# 049. Drop the Whisper CUDA Runtime (Vulkan-only GPU Acceleration)

## Context

Whisper.net shipped with two GPU backends in Parlotype: `Whisper.net.Runtime.Cuda`
(NVIDIA-only, gated behind the `EnableCuda` MSBuild flag, ADR-012) and
`Whisper.net.Runtime.Vulkan` (any vendor, always included, ADR-022). ADR-031 turned
that flag into the **Full** / **Lite** release split.

Three things changed the cost/benefit since ADR-012 was written:

1. **Whisper is no longer the default engine.** Parakeet TDT v3 via sherpa-onnx is
   (ADR-041/042), and it is CPU-only by design. GPU acceleration now affects a
   shrinking subset of users.
2. **The "Full" build was never self-sufficient.** `Whisper.net.Runtime.Cuda` ships
   only `ggml-cuda-whisper.dll` — it does not bundle `cudart`/`cublas`, so the user
   still had to install the multi-gigabyte NVIDIA CUDA toolkit. Half of the Runtime
   settings page existed to explain that, with a "Download CUDA Toolkit" button.
3. **Vulkan measured well.** On the LibriSpeech test-other benchmark
   (`results/comparison-libri-speech-test-other-2026-05-23-cuda.md`, same models, same
   dataset, warm-up per ADR-031-warmup), CUDA is 8–26% faster by RTF, but `Small` and
   `Medium` produce **identical WER**, and `LargeV3Turbo` is actually *better* on
   Vulkan (10.15% vs 11.48% WER).

The flip side is real and measured: CUDA keeps model weights in VRAM, so host RAM was
30–60% lower (`Medium`: 464 MiB CUDA vs 1261 MiB Vulkan). Dropping CUDA moves that
memory back into the process for NVIDIA users on the larger models.

## Decision

Remove the CUDA runtime entirely. There is no `EnableCuda` flag, no CUDA-capable build
from source, and no Full/Lite split.

1. **Packaging** — `Whisper.net.Runtime.Cuda` and the `EnableCuda` property are gone
   from `Parlotype.Platform.csproj`. `Whisper.net.Runtime.Vulkan` remains unconditional.
2. **Core contract** — `RuntimePreference` loses its `Cuda` member and is now
   `Auto / Vulkan / Cpu`. `Auto` chains **Vulkan → CPU**.
3. **Platform** — `WhisperRuntimeBootstrap` drops the CUDA branch from both
   `Initialize` and `IsSatisfiedBy`. `WhisperSpeechRecognizer` no longer takes
   `INvidiaEnvironmentProvider`; its pre-flight check covers Vulkan only.
4. **NVIDIA detection stays** — `INvidiaEnvironmentProvider` (ADR-014) keeps its Core
   interface, its Windows implementation, and its startup log line in `App.axaml.cs`.
   It costs nothing in the artifact, the GPU/driver line is valuable in bug reports,
   and Gemma 4 still runs on CUDA `llama-server` builds where that detection has a
   future. Its CUDA-readiness *UI* is gone.
5. **Settings migration** — a `settings.json` written before this ADR can still hold
   `"Cuda"`. Every reader already degrades an unparseable value to `Auto`;
   `RuntimeSettingsViewModel` additionally rewrites the stale value so it does not
   linger.
6. **Release** — `release.yml` loses its build matrix and publishes a single
   `Parlotype-<version>-win-x64.zip`.

## Consequences

- **Easier:** One download instead of two, and no "which build do I need?" table. The
  Runtime settings page loses the NVIDIA-driver and CUDA-toolkit guidance panels
  entirely — they were the most convoluted part of Settings.
- **Easier:** Release CI halves — one restore/test/publish leg instead of two.
- **Easier:** Users no longer need a ~3 GB CUDA toolkit install to get the build they
  downloaded to work as advertised.
- **Harder:** NVIDIA users lose 8–26% Whisper decode speed, and host RAM grows by
  ~800 MiB on `Medium`/`LargeV3Turbo` because weights are no longer in VRAM. Users who
  feel that can pick a smaller model or stay on the Parakeet default.
- **Harder:** Vulkan is the only GPU path, so a machine with a broken/absent Vulkan
  loader now falls back to CPU where CUDA might previously have worked. `Auto` still
  degrades silently; only the explicit `Vulkan` pick fails loudly.
- **Breaking:** Release asset names lose their `-full` / `-lite` suffix. Links to past
  releases keep working; scripted URL templates need updating.
- **Note:** Measured self-contained `win-x64` output after this change: **731 MB**. The
  CUDA package previously added ~150 MB on top of that (`ggml-cuda-whisper.dll`).
- **Note:** The largest single file in the published output is now
  `onnxruntime_providers_cuda.dll` at **391 MB**, shipped by `org.k2fsa.sherpa.onnx`.
  It is never loaded — `ParakeetSpeechRecognizer` sets `Provider = "cpu"` — so removing
  it is a much bigger win than this ADR. Deferred to its own change because it needs an
  end-to-end Parakeet run to verify.
