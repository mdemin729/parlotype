---
title: "Whisper.net native crash on Linux test hosts"
area: "speech-engines"
summary: "Whisper.net's native ggml library aborts the .NET test host on Linux for both CPU and Vulkan, so benchmark.yml runs on windows-latest."
verified: 2026-11-11
---

# Whisper.net / ggml hard-crashes the test host on Linux

`dotnet test` on Linux (reproduced in WSL Ubuntu 22.04 + .NET 10 SDK; Docker Desktop was
unavailable to cross-check against a true container) hard-aborts the whole test process, not
with a catchable .NET exception, the moment any test constructs a real
`WhisperSpeechRecognizer` and calls `InitializeAsync`:

```
GGML_ASSERT(prev != ggml_uncaught_exception) failed
```

The stack trace bottoms out in `dlopen` loading `runtimes/linux-x64/libggml-base-whisper.so`.

**This is not about GPU runtime selection.** The first suspect was the Vulkan probe under
`RuntimePreference.Auto` (no Vulkan ICD on CI runners), but the crash reproduces identically
with `RuntimePreference.Cpu` forced: same native library, same crash, for CPU and Vulkan
alike. It is a deeper Whisper.net/ggml native compatibility issue with this Linux
environment; root cause not identified.

**Consequence:** `.github/workflows/benchmark.yml` is pinned to `runs-on: windows-latest`
(was `ubuntu-latest`) rather than fixed at the test/library level. Revisit only if a newer
Whisper.net version resolves it upstream, or someone can reproduce or rule it out on a real
GitHub-hosted Linux runner (not WSL) to confirm it is not a WSL-kernel/CPU-flag artifact.

A related test-isolation fact: `WhisperRuntimeBootstrap.Reset()` (test-only) cannot reset
Whisper.net's own `RuntimeOptions.LoadedLibrary`; once any test in the process builds a
`WhisperFactory`, it stays set.
