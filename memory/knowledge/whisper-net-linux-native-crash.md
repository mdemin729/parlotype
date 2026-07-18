---
type: knowledge
tags: [whisper-net, ggml, linux, ci, crash]
created: 2026-07-18
summary: Whisper.net's native ggml library hard-crashes the .NET test host on Linux, independent of CPU/CUDA/Vulkan runtime selection — benchmark.yml is pinned to windows-latest until this is resolved
---

# Whisper.net / ggml hard-crashes the test host on Linux

`dotnet test` on Linux (reproduced in WSL Ubuntu 22.04 + .NET 10 SDK,
Docker Desktop unavailable to cross-check against a true container) hard-aborts
the whole test process — not a catchable .NET exception — the moment any test
constructs a real `WhisperSpeechRecognizer` and calls `InitializeAsync`:

```
GGML_ASSERT(prev != ggml_uncaught_exception) failed
```

stack trace bottoms out in `dlopen` loading
`runtimes/linux-x64/libggml-base-whisper.so`.

**This is not about GPU runtime selection.** Initially suspected the Vulkan
probe under `RuntimePreference.Auto` (no Vulkan ICD on CI runners), but the
crash reproduces identically with `RuntimePreference.Cpu` explicitly forced —
same native library, same crash. It's a deeper Whisper.net/ggml native
compatibility issue with this Linux environment, root cause not identified.

**Consequence:** `.github/workflows/benchmark.yml` is pinned to
`runs-on: windows-latest` (was `ubuntu-latest`) rather than fixed at the
test/library level. Revisit only if: a newer Whisper.net version resolves it
upstream, or someone can reproduce/rule this out on a real GitHub-hosted
Linux runner (not WSL) to confirm it isn't a WSL-kernel/CPU-flag artifact.

**Two smaller, real, unrelated bugs found during the same investigation**
(both fixed, see [[naudio-resampler-read-cost]] neighbor entries in this
vault for the unrelated audio-pipeline knowledge from the same week):
- `WindowsNvidiaEnvironmentProvider.ExtractVersionFromCudaPath` used `Path.*`
  APIs that adapt to the host OS separator, silently failing to parse a
  Windows-style test path (`C:\...`) on Linux.
- `WhisperRuntimeBootstrap.Reset()` (test-only) only clears Parlotype's own
  `_initialized` flag — it never resets Whisper.net's own
  `RuntimeOptions.LoadedLibrary`, which has no reset mechanism at all once a
  factory has loaded. `WhisperRuntimeFallbackTests.LoadedRuntime_IsNull_
  BeforeAnyFactoryCreation` implicitly assumes it's the first test in the
  *entire process* to ever create a `WhisperFactory` — genuinely flaky
  pre-existing test, present on master, unrelated to this crash. Not fixed;
  flagging for whoever touches that file next.
