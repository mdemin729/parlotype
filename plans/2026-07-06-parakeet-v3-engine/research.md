# Research: NVIDIA Parakeet TDT 0.6B v3 as a Parlotype Speech Engine

Date: 2026-07-06

## The model

[nvidia/parakeet-tdt-0.6b-v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3) —
600 M-parameter FastConformer encoder + TDT (Token-and-Duration Transducer) decoder.

| Property | Value |
|----------|-------|
| Languages | 25 European languages (en, es, fr, ru, de, it, pl, uk, ro, nl, hu, el, sv, cs, bg, pt, sk, hr, da, fi, lt, sl, lv, et, mt) with **automatic language detection** — no prompt needed |
| Accuracy | ~6.34 % avg WER on the HuggingFace Open ASR Leaderboard (multilingual); beats Whisper large-v3 on English while being dramatically faster |
| Speed | Among the highest-throughput models on the leaderboard. INT8 on CPU runs many× real-time — a few-second utterance decodes in well under a second, no GPU required |
| Output | Punctuation + capitalization **natively** (unlike raw CTC models); word-level timestamps available |
| Input | 16 kHz mono float PCM — exactly what Parlotype's pipeline already produces |
| Long audio | Up to ~24 min per invocation (local attention); irrelevant for us since VAD segments are short |
| Task | **Transcribe-only.** No translation task (unlike Whisper's translate-to-English or Gemma's arbitrary translation) |
| License | CC-BY-4.0 — commercially friendly |
| Training | NVIDIA Granary dataset, ~670 k hours |

Reference: [Canary-1B-v2 & Parakeet-TDT-0.6B-v3 paper](https://arxiv.org/pdf/2509.14128).

## Runtime options for .NET

### Option A — sherpa-onnx C# bindings (recommended)

[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) (Next-gen Kaldi) ships official
.NET bindings as the [`org.k2fsa.sherpa.onnx`](https://www.nuget.org/packages/org.k2fsa.sherpa.onnx)
NuGet package (v1.13.x as of writing), which pulls per-RID native runtime packages
(`org.k2fsa.sherpa.onnx.runtime.win-x64`, linux-x64, osx). The `OfflineRecognizer`
API supports NeMo transducer models directly:

```
OfflineRecognizerConfig
 └─ ModelConfig
     ├─ Transducer { Encoder, Decoder, Joiner }   // three .onnx files
     ├─ Tokens = tokens.txt
     ├─ ModelType = "nemo_transducer"
     ├─ NumThreads, Provider = "cpu"
```

Pre-converted, tested model artifacts already exist:

- HF repo [csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8](https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8)
  (individual files — ideal for our existing per-file HTTP downloader):
  - `encoder.int8.onnx` — 652 MB
  - `decoder.int8.onnx` — 11.8 MB
  - `joiner.int8.onnx` — 6.4 MB
  - `tokens.txt` — 94 KB
  - **Total ≈ 670 MB** (vs ~6 GB for Gemma 4, 1.5 GB for Whisper medium)
- Same files as a tar.bz2 in [sherpa-onnx release assets](https://github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models)
  ([docs page with verified run parameters](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/offline-transducer/nemo-transducer-models.html)).

Pros: in-process (no sidecar like llama-server), official C# API, cross-platform
native libs from NuGet, model files pre-converted and community-tested, download
pattern identical to `Gemma4ModelDownloadService`.

Cons / constraints:
- **CPU-only from NuGet.** Pre-built packages have no CUDA
  ([issue #1044](https://github.com/k2-fsa/sherpa-onnx/issues/1044),
  [#1313](https://github.com/k2-fsa/sherpa-onnx/issues/1313)) — GPU requires
  building sherpa-onnx from source. Acceptable: the whole point of Parakeet is
  that INT8 CPU inference is already faster than GPU Whisper for our short
  utterances, and it gives AMD/Intel users a fast engine without CUDA/Vulkan.
- Only the INT8 quantization is published in sherpa-onnx form today. INT8 WER
  degradation vs fp32 is small; can revisit if benchmarks show otherwise.
- Adds a native dependency (onnxruntime + sherpa-onnx dlls) to the output.

### Option B — hand-rolled ONNX Runtime (Microsoft.ML.OnnxRuntime)

Use [istupakov/parakeet-tdt-0.6b-v3-onnx](https://huggingface.co/istupakov/parakeet-tdt-0.6b-v3-onnx)
exports and implement log-mel preprocessing + TDT greedy decoding ourselves in C#.
Full control, DirectML GPU possible, but we'd own feature extraction and the
transducer decode loop — significant engineering and correctness risk for little
v1 gain. Keep as a future option if we outgrow sherpa-onnx.

### Option C — NeMo / Python sidecar

Rejected. ADR-024/025 already established the sidecar pain (process lifecycle,
ports, cold start); a Python runtime dependency is worse than llama-server, and
llama.cpp itself does not support Parakeet.

**Decision: Option A.**

## Fit with the existing architecture

The codebase was explicitly built for this:

- `SpeechEngine` enum + `SpeechRecognizerFactory` + `DelegatingSpeechRecognizer`
  make a third engine a switch-case addition.
- `SpeechEngineCapabilities.For()` already documents its fallback branch as
  *"what a future transcribe-only engine (no translation task at all, e.g.
  Parakeet-style ASR) would declare"* — `TranslationForm.None` renders a disabled
  target with an explanatory note in the Language page.
- `Gemma4ModelInfo` / `Gemma4ModelDownloadService` / `Gemma4ModelSettingsViewModel`
  (ADR-029) are a direct template for the Parakeet model catalog + download UI,
  including cumulative multi-file progress — Parakeet needs 4 files, Gemma needs 2.
- Audio pipeline hands recognizers 16 kHz mono float arrays — Parakeet's native
  input format; no resampling work.

### Behavioural notes for the design

- **Source language cannot be forced.** Parakeet v3 always auto-detects; there is
  no language parameter. Capabilities should advertise auto-detect and the
  25-language set, and the recognizer ignores `SelectedSourceLanguage` (surface a
  small UI note, mirroring how Whisper-only options are handled elsewhere).
- **No translation.** `TranslationForm.None` path — already supported by the UI.
- `TranscriptionResult.DetectedLanguage`: sherpa-onnx's offline result exposes a
  `Lang` field but it may be empty for NeMo models — treat as optional, verify
  during implementation.
- **Threading:** sherpa-onnx `OfflineRecognizer` decode is synchronous/blocking —
  wrap in `Task.Run`; create one `OfflineStream` per transcription. Model load
  (652 MB encoder) takes a few seconds — plays well with the existing prewarm +
  loading-spinner work (ADR-038).
- `WhisperOptions` overload of `InitializeAsync` has a default interface
  implementation delegating to the parameterless one — Parakeet recognizer only
  needs the no-args path (benchmark may want an explicit thread-count option).

## Risks

| Risk | Mitigation |
|------|------------|
| sherpa-onnx NuGet or its native runtime emits build warnings (repo builds with `TreatWarningsAsErrors`) | Validate in a spike build first; `NoWarn`/`SuppressDependency` only as last resort |
| INT8 accuracy below expectations on real dictation | Benchmark vs Whisper medium before shipping; the Benchmark CLI integration is part of the plan |
| Native dll size bloats output | onnxruntime + sherpa native ≈ tens of MB — negligible next to the 350 MB CUDA runtime we already ship |
| sherpa-onnx C API surprises (error handling via null returns, UTF-8 marshalling) | Keep recognizer thin; integration test gated on model presence like existing Whisper strict-runtime tests |
| Non-Latin/CJK languages unsupported | Not a regression — engine choice stays per-user; Whisper remains default |
