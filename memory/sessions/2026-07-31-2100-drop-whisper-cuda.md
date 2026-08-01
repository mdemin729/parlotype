---
title: "Session: 2026-07-31 — Drop the Whisper CUDA runtime"
type: session
status: active
tags: [cuda, vulkan, whisper, packaging, release, adr-049]
created: 2026-07-31
summary: Removed CUDA entirely — package, EnableCuda flag, RuntimePreference.Cuda, the Settings guidance panels and the Full/Lite release split. Vulkan is the only GPU runtime. ADR-049.
---

# Session: 2026-07-31

## Active Focus

Branch `claude/whisper-cuda-to-vulkan-4d626c`, rebased onto `master` (fast-forward
onto ADR-048, which had just reworked the same `WhisperSpeechRecognizer` code).

- **Core**: `RuntimePreference` is now `Auto / Vulkan / Cpu`.
- **Platform**: `Whisper.net.Runtime.Cuda` + the `EnableCuda` property removed from
  `Parlotype.Platform.csproj`; `WhisperRuntimeBootstrap` drops the CUDA branch from
  `Initialize` and `IsSatisfiedBy` (`Auto` = `[Vulkan, Cpu]`); `WhisperSpeechRecognizer`
  no longer takes `INvidiaEnvironmentProvider` and its pre-flight covers Vulkan only.
- **Desktop**: `RuntimeSettingsViewModel` loses the CUDA option, `CudaDriverMissing` /
  `CudaSdkMissing` / `CudaDriverVersion`, `OpenCudaDownloadLinkCommand` and the NVIDIA
  dependency; `RuntimeSettingsView.axaml` loses both CUDA guidance panels. New: a stale
  `"Cuda"` in `settings.json` is rewritten to `Auto` on load.
- **Benchmark**: `--gpu` help text; three `datasets/whisper-*-libri-speech-*-config.json`
  had `"runtimePreference": "Cuda"` and would have failed to deserialize.
- **CI**: `release.yml` loses the Full/Lite matrix — one `Parlotype-<version>-win-x64.zip`.
- **Tests**: bootstrap/fallback/latch/strict-runtime suites rewritten; deleted the now
  consumerless `MockNvidiaEnvironmentProvider` (Desktop.Tests). 1023 tests green.

## Decisions Made

- **CUDA removed completely**, not flag-defaulted-off. User's call: "REMOVE CUDA
  Completely. Keep the code clean." No CUDA-capable build from source remains.
- **`INvidiaEnvironmentProvider` stays** (user's call). Its Core interface, Windows
  implementation and startup log line survive; only the Settings UI that consumed it is
  gone. Costs nothing in the artifact and the GPU/driver line is useful in bug reports.
- **Settings migration is a rewrite, not just a fallback.** Every reader already degraded
  an unparseable value to `Auto`; the VM additionally persists the normalized value so
  `"Cuda"` cannot linger in `settings.json` and confuse a later reader.
- **Latch tests keep one `RuntimeLibrary.Cuda` case** — a runtime we no longer package
  can still be latched by a stale process, and `IsSatisfiedBy` must reject it.
- Release asset names lose their `-full`/`-lite` suffix. Accepted as breaking for scripted
  URL templates; past releases keep their own names.

## Facts Learned

- **`onnxruntime_providers_cuda.dll` is 391 MB — ~54% of the entire 731 MB published
  output** — and is never loaded, because `ParakeetSpeechRecognizer` pins
  `Provider = "cpu"` (ADR-041). It arrives via `org.k2fsa.sherpa.onnx`. Removing it is a
  far bigger win than this whole ADR; deferred because it needs an end-to-end Parakeet
  run to verify. Recorded in [[sherpa-onnx-quirks]].
- `RuntimeLibrary.Cuda` still exists in Whisper.net's enum after the package is gone, so
  test code referencing it keeps compiling — useful for the stale-latch case above.
- The benchmark's `whisperRuntime` field in `results/*.json` is a plain string, so
  historical runs and the `import` command survive an enum member removal untouched;
  `BenchmarkConfig.RuntimePreference` and `SweepExpander`'s `Enum.Parse` do not.

## Open Blockers

None. Not verified on real hardware in this session: an actual Vulkan transcription on a
GPU host (the change cannot alter the Vulkan path, but the app was not launched).

## Documentation Status

- ADR: **done** — `docs/decisions/049-drop-whisper-cuda-runtime.md`; supersede/amend
  banners added to ADR-012 (superseded), 014, 022, 031.
- Vault (services/architecture): **done** — `decisions/_index`, `architecture/subsystems`,
  `architecture/dependency-graph`, `conventions/dotnet-standards`,
  `conventions/testing-strategy`, `services/{core,platform,desktop,tests}`, `memory/CLAUDE.md`.
- Knowledge: **done** — [[whisper-cuda-runtime-packaging]] marked historical with the
  post-removal measurement; [[sherpa-onnx-quirks]] gained the 391 MB finding.

## Next Action

**Strip the unused ONNX Runtime GPU providers from the published output** — exclude
`onnxruntime_providers_cuda.dll` (391 MB) and `onnxruntime_providers_tensorrt.dll` from
`org.k2fsa.sherpa.onnx`'s native assets, keeping `onnxruntime.dll`,
`onnxruntime_providers_shared.dll` and `Microsoft.ML.OnnxRuntime.dll`. Then launch the
app with the default Parakeet engine, record a clip, and confirm transcription still
works with no native-load error in `%LOCALAPPDATA%/parlotype/logs/`. That single change
roughly halves the download — needs its own ADR.

Also outstanding, in the separate `parlotype-website` repo: the download page still
documents the Full/Lite split (EN + RU).
