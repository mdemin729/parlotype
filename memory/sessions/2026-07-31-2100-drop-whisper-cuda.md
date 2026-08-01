---
title: "Session: 2026-07-31 — Drop the Whisper CUDA runtime and the ONNX GPU providers"
type: session
status: active
tags: [cuda, vulkan, whisper, packaging, release, onnxruntime, adr-049, adr-050]
created: 2026-07-31
summary: Removed CUDA entirely — package, EnableCuda flag, RuntimePreference.Cuda, the Settings guidance panels and the Full/Lite release split (ADR-049). Then cut the published artifact 731 MB → 338 MB by filtering the never-loaded ONNX Runtime GPU providers (ADR-050).
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
- **Packaging (ADR-050)**: new root `Directory.Build.targets` filters the ONNX Runtime
  CUDA/TensorRT provider natives out of `ReferenceCopyLocalPaths` and
  `ResolvedFileToPublish`. Published `win-x64` output **731 MB → 338 MB**.

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

- **`onnxruntime_providers_cuda.dll` is 391 MB — ~54% of the 731 MB published output**
  and is never loaded. First assumed to come from `org.k2fsa.sherpa.onnx`; it actually
  arrives via **`SileroVad 1.3.0` → `Microsoft.ML.OnnxRuntime.Gpu 1.18.1`**, a dependency
  no `.csproj` in the repo names. Removed in ADR-050 the same session (731 MB → 338 MB).
  Full write-up in [[onnxruntime-gpu-providers-dead-weight]].
- The providers could not have loaded even if requested: they target ORT 1.18.1, while the
  `onnxruntime.dll` that wins the output folder is sherpa-onnx’s own newer native build.
  Proven inert by running the Parakeet smoke benchmark with and without the DLL present —
  identical WER 6.4% / CER 2.6% / RAM Δ 46.1 MB.
- `RuntimeLibrary.Cuda` still exists in Whisper.net's enum after the package is gone, so
  test code referencing it keeps compiling — useful for the stale-latch case above.
- The benchmark's `whisperRuntime` field in `results/*.json` is a plain string, so
  historical runs and the `import` command survive an enum member removal untouched;
  `BenchmarkConfig.RuntimePreference` and `SweepExpander`'s `Enum.Parse` do not.

## Open Blockers

None. Not verified on real hardware in this session: an actual Vulkan transcription on a
GPU host (the change cannot alter the Vulkan path, but the app was not launched).

## Documentation Status

- ADR: **done** — `docs/decisions/049-drop-whisper-cuda-runtime.md` and
  `docs/decisions/050-drop-onnx-runtime-gpu-providers.md`; supersede/amend banners added
  to ADR-012 (superseded), 014, 022, 031.
- Vault (services/architecture): **done** — `decisions/_index`, `architecture/subsystems`,
  `architecture/dependency-graph`, `conventions/dotnet-standards`,
  `conventions/testing-strategy`, `services/{core,platform,desktop,tests}`, `memory/CLAUDE.md`.
- Knowledge: **done** — [[whisper-cuda-runtime-packaging]] marked historical with the
  post-removal measurement; new [[onnxruntime-gpu-providers-dead-weight]];
  [[sherpa-onnx-quirks]] gained the "sherpa wins the onnxruntime.dll race" note.

## Next Action

**Trim the ~100 MB of native PDBs from release publishes.** `libSkiaSharp.pdb` (80 MB) and
`libHarfBuzzSharp.pdb` (20 MB) are now the two largest files in the 338 MB output — they
come from SkiaSharp/HarfBuzzSharp native packages and are debug symbols no end user needs.
The same `Directory.Build.targets` filter pattern ADR-050 introduced applies, but it should
be scoped to Release/publish only so local debugging keeps its symbols. Verify by
publishing and confirming the app still starts and renders.

Also outstanding, in the separate `parlotype-website` repo: the download page still
documents the Full/Lite split and the old size (EN + RU).
