---
title: "Benchmark measurement caveats"
area: "benchmark"
summary: "Peak RAM includes warm-up and omits llama-server, WhisperRuntime reads \"unknown\" outside Whisper, and sweep runs whatever engine settings.json selects."
verified: 2026-11-11
---

# Benchmark measurement caveats

Four caveats about `Parlotype.Benchmark` output that the numbers themselves do not
advertise. Some make cross-engine comparisons meaningless rather than merely noisy.

## 1. Peak RAM is a process high-water mark, so warm-up is inside it

Per-sample memory comes from `Process.PeakWorkingSet64`, which never falls. Once the
warm-up pass ([ADR-071](../decisions/071-benchmark-warmup-pass.md)) has run its throwaway
transcription, its allocation is baked into the peak for **every** sample that follows.
Per-sample RAM is therefore an upper bound on the whole run to date, not a measurement of
that sample.

## 2. Child-process memory is invisible

For llama.cpp/Gemma the model lives in the spawned `llama-server.exe`, which is a
**separate process** and contributes nothing to Parlotype's working set. The engine with by
far the largest real footprint reports the smallest. Comparing Gemma's memory column
against Whisper's or Parakeet's compares a sidecar's client to an in-process model.
External-process tracking was never wired.

## 3. `WhisperRuntime` is Whisper-only despite being a general field

`EnvironmentInfo.WhisperRuntime` is populated as:

```csharp
WhisperRuntime = RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown",
```

That static belongs to Whisper.net and is only set once a `WhisperFactory` has been built
([vulkan-runtime-probing](vulkan-runtime-probing.md) §3). Any llama.cpp, Parakeet or cloud
run stores the literal `"unknown"`, which is then shown as "Runtime" in `ConsoleReporter`,
persisted to SQLite, and compared as `RuntimeA`/`RuntimeB` by `ResultComparer`. It is a
missing value, not a detected one.

Observed 2026-07-31: even a successful **Whisper** run printed "Runtime: unknown", i.e.
`LoadedLibrary` was still null after the model loaded inside the benchmark. Since
`WhisperRuntimeBootstrap.IsSatisfiedBy` treats a null loaded runtime as satisfied, the
runtime-latch guard ([ADR-048](../decisions/048-whisper-runtime-latch-and-factory-lifetime.md))
passes vacuously there and may never fire outside the desktop app.

Related schema facts: `whisperRuntime` in `results/*.json` is a plain string, so historical
runs and the `import` command survive removing a runtime enum member untouched;
`BenchmarkConfig.RuntimePreference` and `SweepExpander`'s `Enum.Parse<RuntimePreference>` do
not. `RuntimeLibrary.Cuda` still exists in Whisper.net's own enum after the CUDA package was
dropped, so test code that references it keeps compiling.

## 4. `sweep` benchmarks whatever engine the app has selected

`ISpeechRecognizer` resolves to `DelegatingSpeechRecognizer`, which asks
`SpeechRecognizerFactory` for the engine named by `SpeechEngine` in the **user's real
`settings.json`**. A benchmark that resolves `ISpeechRecognizer` instead of a concrete
recognizer therefore runs the app's current engine, whatever the config says. Since
Parakeet became the default ([ADR-041](../decisions/041-parakeet-v3-sherpa-onnx.md)), a
config saying `"whisper": { "model": "Base" }` produces Parakeet numbers under a Whisper
label. Nothing warns, and the summary still prints the Whisper model name.

`run` was fixed on 2026-08-18: its Whisper branch pins `SpeechEngine`,
`SelectedWhisperModel` and `RuntimePreference` in an `InMemorySettingsService` and resolves
`WhisperSpeechRecognizer` directly, like the Parakeet and llama.cpp branches. **`sweep` was
not**: `Program.cs` still calls `GetRequiredService<ISpeechRecognizer>()` with no settings
override. Whisper results in `results/` produced between ADR-041 and 2026-08-18 are suspect.

The tell is a stack trace naming the wrong recognizer, or an implausible metric jump between
runs of the "same" config.

## Historical: Gemma Q8_0 instability

`gemma-4-E2B-it-Q8_0` intermittently emitted stray `<|channel>` tokens mid-reasoning that
crashed llama-server's chat-template parser with HTTP 500, forcing a retry to obtain a clean
50-sample run; the reasoning bleed-through also pushed RTF to 0.315 against a normal ~0.04.
Observed 2026-05-23, never root-caused (suspected sampler/prompt tuning). A future Gemma
benchmark hitting HTTP 500s should suspect the quant before the harness.

Related: [llama-server-hf-download](llama-server-hf-download.md).
