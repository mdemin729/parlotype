---
title: "SileroVad 2.x is locked to ONNX Runtime 1.30"
area: "audio"
summary: "SileroVad 2.x throws at startup unless the native onnxruntime.dll is exactly the version of its managed ONNX Runtime (1.30.0); sherpa-onnx 2.0.3 ships 1.28.2, so the two cannot share a process as packaged."
verified: 2026-11-23
---

# SileroVad 2.x is locked to ONNX Runtime 1.30

Found while trying SileroVad 2.2.0 next to sherpa-onnx 2.0.3
([ADR-077](../decisions/077-dependency-refresh-naudio3-sharphook8-sherpa2.md)).

- SileroVad 1.3.0 depends on `Microsoft.ML.OnnxRuntime.Gpu` 1.18.1 and brings a native
  runtime with it. SileroVad 2.0.0, 2.1.0 and 2.2.0 depend only on
  `Microsoft.ML.OnnxRuntime.Managed` 1.30.0 and expect the application to supply the native
  library.
- `new Vad()` calls `VadRuntime.EnsureNativeRuntimeAvailable()`, which compares the file
  version of the `onnxruntime.dll` in the output folder with the managed assembly. A mismatch
  throws `InvalidOperationException` ("The native ONNX Runtime at ... is version 1.28.2 but the
  managed one is 1.30.0.0") before any inference.
- The only `onnxruntime.dll` in our output is the one from
  `org.k2fsa.sherpa.onnx.runtime.<rid>`: 1.24.4 in sherpa-onnx 1.13.3, 1.28.2 in 2.0.3. There is
  no `Microsoft.ML.OnnxRuntime.Managed` 1.28.2 to pin down to (1.28.0, then 1.29.0).
- So SileroVad 2.x needs `Microsoft.ML.OnnxRuntime` 1.30.0 added, which puts a second
  `onnxruntime.dll` at the same output path and makes sherpa-onnx run on a runtime it was not
  built against.
- Separately, `new Vad()` in 2.x loads the v5-layout model (silero-vad v6.3, fixed 512-sample
  window) instead of v4; `new Vad(VadModelKind.V4)` keeps the old model. The thresholds in
  [`vad-tuning`](vad-tuning.md) were tuned on v4 with a 1024-sample window.

The symptom in this repo: the four `SileroVadServiceTests` and
`AudioPipelineTests.Pipeline_WithVadAndWhisper_ProducesTranscription` fail; everything else
passes, because nothing else constructs a real `Vad`.
