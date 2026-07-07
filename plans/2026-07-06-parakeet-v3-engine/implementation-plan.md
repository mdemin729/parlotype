# Implementation Plan: Parakeet TDT 0.6B v3 Engine

Companion to [task.md](task.md); background in [research.md](research.md).
Follows the contract-first workflow (Core → Platform → DI → Desktop → tests → ADR).

## Phase 0 — Spike (de-risk before touching the solution)

Standalone throwaway console project (outside the solution) that:

1. References `org.k2fsa.sherpa.onnx` (pin latest stable, ~1.13.x).
2. Loads the int8 model from a local folder and transcribes
   `sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/test_wavs/*`.
3. Confirms: output text quality (punctuation/caps), decode latency for a ~5 s
   clip, whether `result.Lang` is populated, memory footprint after load,
   and that the package restores/builds **without warnings** on net10.0
   (`TreatWarningsAsErrors` compatibility).

Config shape expected:

```csharp
var config = new OfflineRecognizerConfig();
config.ModelConfig.Transducer.Encoder = ".../encoder.int8.onnx";
config.ModelConfig.Transducer.Decoder = ".../decoder.int8.onnx";
config.ModelConfig.Transducer.Joiner  = ".../joiner.int8.onnx";
config.ModelConfig.Tokens             = ".../tokens.txt";
config.ModelConfig.ModelType          = "nemo_transducer";
config.ModelConfig.NumThreads         = Math.Max(1, Environment.ProcessorCount / 2);
using var recognizer = new OfflineRecognizer(config);
using var stream = recognizer.CreateStream();
stream.AcceptWaveform(16000, samples);   // float[] in [-1,1]
recognizer.Decode(stream);
var text = stream.Result.Text;
```

**Gate:** if the spike fails (warnings, crashes, bad output), stop and re-plan
(fallback: Option B in research.md — raw ONNX Runtime).

## Phase 1 — Core contracts

1. `SpeechEngine.cs` — add `Parakeet` member (append; enum persisted by name,
   not ordinal, so ordering is free but append anyway):
   ```csharp
   /// <summary>NVIDIA Parakeet TDT 0.6B v3 via sherpa-onnx. CPU, fast, 25 European languages.</summary>
   Parakeet
   ```
2. New `Parlotype.Core/Speech/ParakeetModelInfo.cs` — mirror `Gemma4ModelInfo`:
   record with `ModelId`, `DisplayName`, `HuggingFaceRepo`, file names
   (`EncoderFileName`, `DecoderFileName`, `JoinerFileName`, `TokensFileName`),
   `DiskSize`. Single catalog entry for now:
   - `TdtV3Int8` — repo `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8`,
     `encoder.int8.onnx` / `decoder.int8.onnx` / `joiner.int8.onnx` /
     `tokens.txt`, "~670 MB". `Default => TdtV3Int8`, `All`, `GetById`.
   - Cache dir: reuse `%LOCALAPPDATA%\parlotype\models\` but nest per-model
     (`models/parakeet-tdt-0.6b-v3-int8/`) since file names are generic
     (`encoder.int8.onnx` would collide with a future v2/en model).
3. `SettingsKeys.cs` — add `SelectedParakeetModel` (future-proofing for an
   English-only v2 or fp32 variant).
4. `LanguageCatalog` — add `ParakeetLanguages` (the 25 codes: en, es, fr, ru,
   de, it, pl, uk, ro, nl, hu, el, sv, cs, bg, pt, sk, hr, da, fi, lt, sl, lv,
   et, mt), filtered from `AllLanguages` the same way `WhisperLanguages` is built.
5. `SpeechEngineCapabilities.For()` — explicit `Parakeet` branch:
   ```csharp
   SpeechEngine.Parakeet => new LanguageCapabilities(
       SupportsAutoDetect: true,
       SupportedSourceLanguages: LanguageCatalog.ParakeetLanguages,
       SupportsArbitraryTranslation: false,
       FixedTranslationTargets: []),   // TranslationForm.None
   ```
   Note: the engine *always* auto-detects — a picked source language is
   informational only. Decide during Desktop phase whether to disable the source
   picker for Parakeet or keep it selectable-but-ignored with a hint (leaning:
   show the 25 languages greyed with an "auto-detected" note; smallest UI delta).

## Phase 2 — Platform implementation

1. `Parlotype.Platform.csproj` — add `org.k2fsa.sherpa.onnx` PackageReference.
2. New `Speech/ParakeetModelDownloadService.cs` — clone of
   `Gemma4ModelDownloadService` generalized to N files: per-file cache checks
   (`IsModelCached` = all four present), HEAD-based combined size, single
   cumulative `IProgress<ModelDownloadProgress>`, `DeleteModelAsync`. Download
   URL pattern: `https://huggingface.co/{repo}/resolve/main/{file}`.
3. New `Speech/ParakeetSpeechRecognizer.cs : ISpeechRecognizer`:
   - `InitializeAsync`: resolve `ParakeetModelInfo` from
     `SettingsKeys.SelectedParakeetModel` (default `TdtV3Int8`); throw a clear
     "Download it first in Settings → Parakeet Model" error if files missing
     (same UX contract as `LlamaCppSpeechRecognizer`); construct
     `OfflineRecognizer` inside `Task.Run` (encoder load takes seconds);
     guard with `SemaphoreSlim` like the Whisper recognizer.
   - `TranscribeAsync`: new `OfflineStream` per call, `AcceptWaveform(16000, …)`,
     `Decode`, map to `TranscriptionResult { Text, DetectedLanguage = result.Lang
     (null if empty) }`. Run on `Task.Run`; honor cancellation before/after
     decode (sherpa decode itself is not cancellable — segments are short).
   - `UnloadAsync` / `DisposeAsync`: dispose `OfflineRecognizer`, remain
     re-initializable (ADR-017 contract).
   - Ignores `WhisperOptions` overload (default interface impl) and
     `SelectedSourceLanguage`; existing post-processing (profanity filter etc.)
     continues to apply downstream in the pipeline, unchanged.
4. `SpeechRecognizerFactory` — add
   `SpeechEngine.Parakeet => _services.GetRequiredService<ParakeetSpeechRecognizer>()`.
5. `PlatformServiceExtensions.cs` — register `ParakeetSpeechRecognizer` and
   `ParakeetModelDownloadService` as singletons (next to the LlamaCpp block).

## Phase 3 — Desktop UI

1. `SpeechEngineSettingsViewModel` — third `SpeechEngineDisplayItem`:
   *"Parakeet v3 (Fast)"* — "NVIDIA Parakeet via ONNX. ~670 MB download.
   25 European languages, auto-detected. Fastest engine; runs on any CPU, no
   GPU needed. No translation." Add `IsParakeetSelected` observable (pattern:
   `IsGemma4Selected`).
2. New `ParakeetModelSettingsViewModel` + `ParakeetModelSettingsView`
   (Category `SpeechEngine`, `RestrictToEngine = Parakeet`) — model card with
   download state, size, Download/Delete via the existing
   `ModelDownloadDialog` coordination (recording stop + recognizer unload
   before delete), modeled on `Gemma4ModelSettingsViewModel` (ADR-029).
   Register in `App.axaml.cs` DI + `SettingsWindowViewModel` section list.
3. Language page: with `TranslationForm.None` the target side already renders
   disabled with a note (ADR-036 machinery). Verify the source picker shows the
   25-language list and add the "always auto-detected" hint decided in Phase 1.
4. Prewarm (ADR-038) and loading-spinner flows go through
   `DelegatingSpeechRecognizer` — should work unchanged; verify.

## Phase 4 — Benchmark integration

1. `BenchmarkConfig` — add optional `ParakeetConfig` (model id, numThreads),
   `EngineName` → "Parakeet"; validation: at most one engine section set.
2. Pipeline: instantiate `ParakeetSpeechRecognizer` (Platform is already
   referenced) via the same recognizer bootstrapping used for Whisper/Gemma;
   headless model download reuses `ParakeetModelDownloadService`.
3. Add `datasets/parakeet-smoke-config.json`; run the smoke dataset and compare
   WER/CER/RTF against Whisper medium (record numbers in the ADR).
4. Sweep axes (`parakeet.numThreads`) — optional, cheap to include.

## Phase 5 — Tests

- **Parlotype.Tests:** `ParakeetModelInfo` catalog/`GetById`;
  `SpeechEngineCapabilities.For(Parakeet)` shape (auto-detect, 25 languages,
  `TranslationForm.None`); download-service path resolution + cache detection
  (temp dir); recognizer throws informative error when model missing;
  gated integration test (skip when model not cached) transcribing a bundled
  short WAV — pattern: `WhisperSpeechRecognizerStrictRuntimeTests`.
- **Parlotype.Desktop.Tests:** engine options list contains Parakeet;
  selecting it persists `SpeechEngine=Parakeet` and unloads the recognizer;
  Parakeet model section visibility follows `RestrictToEngine`; language page
  renders the no-translation note (headless).
- **Parlotype.Benchmark.Tests:** `ParakeetConfig` deserialization + engine-name
  resolution + mutual-exclusion validation.

## Phase 6 — Docs, ADR, memory vault

- **ADR-041** `parakeet-v3-sherpa-onnx.md`: decision (in-process sherpa-onnx vs
  sidecar vs raw ORT), CPU-only rationale, model source, capability shape,
  benchmark numbers. Triggers fired: new Core enum/record, new DI registrations,
  new dependency, speech-subsystem change.
- Memory vault: `memory/services/parlotype-core.md` + `parlotype-platform.md`
  + `parlotype-desktop.md` symbol lists; `memory/architecture/subsystems.md`
  speech-engine section; `memory/decisions/_index.md` row; knowledge note
  `memory/knowledge/sherpa-onnx.md` (NuGet CPU-only, native lib layout, any
  spike quirks).
- `CLAUDE.md`: mention third engine in Project Overview + settings list.

## Sequencing & estimate

Phases are ordered by dependency; 0→1→2 must be serial, 3/4/5 can interleave.
Rough effort: spike ½ day; Core+Platform 1 day; Desktop 1 day; Benchmark ½ day;
tests+docs 1 day. **~4 developer-days.**

## Out of scope (deliberate)

- GPU (CUDA/DirectML) execution — no pre-built sherpa-onnx GPU NuGet; revisit
  only if CPU latency disappoints (it shouldn't).
- Streaming recognition — parakeet-tdt-v3 has no streaming export in
  sherpa-onnx ([issue #2918](https://github.com/k2-fsa/sherpa-onnx/issues/2918));
  batch mode matches the default pipeline anyway.
- fp32/fp16 model variants and parakeet-tdt-0.6b-**v2** (English-only) — the
  `ParakeetModelInfo` catalog leaves room; add later if benchmarks justify.
- Word-level timestamps — `TranscriptionResult` has no field for them today.
