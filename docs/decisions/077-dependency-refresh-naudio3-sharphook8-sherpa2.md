---
status: accepted
date: 2026-11-23
amends: [003, 020, 041]
---

# 077. Dependency refresh: Avalonia 12.2, NAudio 3, SharpHook 8, sherpa-onnx 2

## Context

The package references had drifted: Avalonia was on 12.0.2, the `Microsoft.Extensions.*`
packages were still a .NET 10 preview, and four libraries on the dictation path had new major
versions. Three of those majors change API the app calls directly, and one of them cannot be
taken at all without changing which ONNX Runtime the process loads.

## Decision

Every package moves to its latest stable release, with two exceptions listed below.

**Audio capture (NAudio 2.2.1 → 3.3.0, amends ADR-003).** `WasapiCapture` is obsolete in
NAudio 3, so `WasapiAudioCaptureService` builds a `WasapiRecorder` through
`WasapiRecorderBuilder` (shared mode, polling, 100 ms buffer: the settings the old constructor
implied). The recorder raises `DataAvailable` once per WASAPI packet, about every 10 ms, where
`WasapiCapture` batched packets into a callback roughly every 60 ms. The resampler read request
is sized from the packet, and its floor drops from 1024 to 64 samples; with the old floor the
six-times-higher callback rate raised allocation during recording from 1.9 MB/s to 6.8 MB/s.
`IMMNotificationClient` is internal in NAudio 3, so `WasapiMicrophoneEnumerator` subscribes to
the events of `MMDeviceEnumerator.CreateNotificationClient()`. NAudio 3 marks its WASAPI types
`[SupportedOSPlatform("windows")]`; the two services carry the same attribute and their
registrations in `PlatformServiceExtensions` suppress CA1416, because they are still registered
on every OS as the only implementations.

**Hotkeys and key simulation (SharpHook 7.1.1 → 8.0.3, amends ADR-020).** The hook is still a
keyboard-only `SimpleGlobalHook` on a background thread; in v8 that is said at
`RunAsync(GlobalHookType.Keyboard, true)` instead of in the constructor, which now takes the
provider. `EventSimulator` moved to `SharpHook.Simulation`, is created with
`EventSimulator.Create("Parlotype")` and is disposable, so both text injection services
dispose it.

**Parakeet (sherpa-onnx 1.13.3 → 2.0.3, amends ADR-041).** No source change. The bundled
`onnxruntime.dll`, the only one in the output folder, moves from 1.24.4 to 1.28.2.

**Everything else** is a version bump with at most a call-site fix: Avalonia 12.2.0,
`Microsoft.Extensions.*` 11.0.0, `System.CommandLine` 3.0.0, Whisper.net 1.9.1, Spectre.Console
0.57.2, BenchmarkDotNet 0.15.8, Velopack 1.2.161, ZLogger 2.6.2, CommunityToolkit.Mvvm 8.4.2,
Microsoft.NET.Test.Sdk 18.12.0, xunit.runner.visualstudio 4.0.1.

**Held back:**

- **SileroVad stays on 1.3.0.** Every 2.x release depends on
  `Microsoft.ML.OnnxRuntime.Managed` 1.30.0 and refuses to start unless the native
  `onnxruntime.dll` is the same version. Ours is the 1.28.2 that sherpa-onnx ships. Taking
  SileroVad 2 means adding a second ONNX Runtime package that overwrites the one sherpa-onnx
  was built and tested against, and it also changes the default VAD model from v4 to v5. Both
  are decisions of their own
  ([knowledge note](../knowledge/silerovad-2-onnxruntime-version-lock.md)).
- **xunit.v3 stays on 3.2.2.** `Avalonia.Headless.XUnit` 12.2.0 is built against it, and
  xunit.v3 4.x no longer runs under VSTest `dotnet test` on the .NET 10 SDK
  ([knowledge note](../knowledge/xunit-v3-4-needs-microsoft-testing-platform.md)).

## Consequences

- Audio subscribers receive 10 ms buffers instead of ~60 ms ones. Total samples, sample rate
  and allocation per second are unchanged (measured on a live microphone: 80,000 samples and
  1.9 MB/s over 5 s, before and after).
- The smoke benchmarks give the same result as before for Whisper (WER 3.6 %) and run
  Parakeet on sherpa-onnx 2.0.3 (WER 2.7 %).
- `Microsoft.Extensions.*` 11.0.0 runs on the `net10.0` target; the target framework itself is
  unchanged.
- SileroVad 1.3.0 still drags in `Microsoft.ML.OnnxRuntime.Gpu` 1.18.1, so the provider filter
  from ADR-050 stays.
- Moving SileroVad or xunit.v3 forward needs the two blockers above resolved first.
